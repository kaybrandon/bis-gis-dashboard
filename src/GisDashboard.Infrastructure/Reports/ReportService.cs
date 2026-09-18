using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using GisDashboard.Application.Abstractions;
using GisDashboard.Application.Email;
using GisDashboard.Application.Exceptions;
using GisDashboard.Application.Reports;
using GisDashboard.Application.WorkItems;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Email;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GisDashboard.Infrastructure.Reports;

public sealed class ReportService : IReportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex ExtraEmailPattern = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IOrgScope _orgScope;
    private readonly IEmailSender _email;
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<ReportService> _logger;

    public ReportService(
        AppDbContext db,
        ICurrentUser currentUser,
        IOrgScope orgScope,
        IEmailSender email,
        IOptions<EmailOptions> emailOptions,
        ILogger<ReportService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _orgScope = orgScope;
        _email = email;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ReportListItem>> ListAsync(Guid? organizationId, CancellationToken cancellationToken = default)
    {
        IQueryable<MonthlyReport> query = _db.MonthlyReports.AsNoTracking();
        if (organizationId is { } orgId && orgId != Guid.Empty)
        {
            await _orgScope.EnsureCanAccessOrganizationAsync(orgId, cancellationToken);
            query = query.Where(r => r.OrganizationId == orgId);
        }
        else
        {
            var allowed = await _orgScope.GetAllowedOrganizationIdsAsync(cancellationToken);
            query = query.Where(r => allowed.Contains(r.OrganizationId));
        }

        var rows = await query
            .OrderByDescending(r => r.Year)
            .ThenByDescending(r => r.Cadence == ReportCadences.Annual)
            .ThenByDescending(r => r.Month)
            .ThenByDescending(r => r.Version)
            .Select(r => new
            {
                r.Id,
                r.OrganizationId,
                OrganizationName = r.Organization.Name,
                r.Cadence,
                r.Year,
                r.Month,
                r.Version,
                r.MonthLabel,
                r.GeneratedAt,
                GeneratedByName = r.GeneratedByUser.FullName != null && r.GeneratedByUser.FullName != ""
                    ? r.GeneratedByUser.FullName
                    : r.GeneratedByUser.DisplayName,
                r.LastEmailedAt,
                r.LastEmailedTo,
                r.EmailCount
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new ReportListItem(
            r.Id,
            r.OrganizationId,
            r.OrganizationName,
            r.Cadence,
            r.Year,
            r.Month,
            r.Version,
            r.MonthLabel,
            r.GeneratedAt,
            r.GeneratedByName,
            r.LastEmailedAt,
            r.LastEmailedTo,
            r.EmailCount,
            r.EmailCount > 0 && r.LastEmailedAt is not null)).ToList();
    }

    public async Task<ReportDetail> GetAsync(Guid reportId, CancellationToken cancellationToken = default)
    {
        var report = await LoadAccessibleAsync(reportId, tracking: false, cancellationToken);
        return MapDetail(report);
    }

    public async Task<ReportDetail> GenerateAsync(GenerateReportRequest request, CancellationToken cancellationToken = default)
    {
        EnsureCanMutate();
        if (request.Year is < 2000 or > 2100)
        {
            throw new ValidationException("Enter a valid year.");
        }

        var annual = ReportCadences.IsAnnual(request.Cadence);
        var month = annual ? 0 : request.Month ?? 0;
        if (!annual && month is < 1 or > 12)
        {
            throw new ValidationException("Enter a valid calendar month.");
        }

        await _orgScope.EnsureCanAccessOrganizationAsync(request.OrganizationId, cancellationToken);
        var organization = await _db.Organizations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken)
            ?? throw new NotFoundException("Organization was not found.");

        var periodStart = annual
            ? new DateTimeOffset(request.Year, 1, 1, 0, 0, 0, TimeSpan.Zero)
            : new DateTimeOffset(request.Year, month, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = annual ? periodStart.AddYears(1) : periodStart.AddMonths(1);
        var cadence = annual ? ReportCadences.Annual : ReportCadences.Monthly;
        var snapshot = await BuildSnapshotAsync(organization, cadence, request.Year, periodStart, periodEnd, cancellationToken);
        var nextVersion = await _db.MonthlyReports
            .Where(r => r.OrganizationId == request.OrganizationId && r.Cadence == cadence && r.Year == request.Year && r.Month == month)
            .Select(r => (int?)r.Version)
            .MaxAsync(cancellationToken) ?? 0;

        var now = DateTimeOffset.UtcNow;
        var report = new MonthlyReport
        {
            Id = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            Year = request.Year,
            Month = month,
            Cadence = cadence,
            Version = nextVersion + 1,
            MonthLabel = snapshot.MonthLabel,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            GeneratedAt = now,
            GeneratedByUserId = _currentUser.UserId,
            SnapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions)
        };

        _db.MonthlyReports.Add(report);
        await _db.SaveChangesAsync(cancellationToken);

        report.Organization = organization;
        report.GeneratedByUser = await _db.Users.AsNoTracking()
            .FirstAsync(u => u.Id == _currentUser.UserId, cancellationToken);
        _logger.LogInformation(
            "Generated {Cadence} report {ReportId} v{Version} for {Org} {Month}",
            cadence,
            report.Id,
            report.Version,
            organization.Name,
            report.MonthLabel);
        return MapDetail(report);
    }

    public async Task<IReadOnlyList<ReportRecipient>> ListRecipientsAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        EnsureCanMutate();
        await _orgScope.EnsureCanAccessOrganizationAsync(organizationId, cancellationToken);

        var rows = await _db.Users
            .AsNoTracking()
            .Where(u => u.IsActive && !u.IsArchived && u.Organizations.Any(m => m.OrganizationId == organizationId))
            .Select(u => new { u.Id, u.FullName, u.DisplayName, u.UserName, u.Email })
            .ToListAsync(cancellationToken);

        return rows
            .Select(u => new ReportRecipient(
                u.Id,
                ReportPersonNames.FromFields(u.FullName, u.DisplayName, u.UserName, u.Email),
                u.Email ?? string.Empty,
                ReportPersonNames.FullNameOrNull(u.FullName)))
            .OrderBy(u => u.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<EmailReportResult> EmailAsync(Guid reportId, EmailReportRequest request, CancellationToken cancellationToken = default)
    {
        EnsureCanMutate();
        var report = await LoadAccessibleAsync(reportId, tracking: true, cancellationToken);

        var userIds = (request.UserIds ?? []).Distinct().ToList();
        var extraEmails = (request.ExtraEmails ?? [])
            .Select(e => e.Trim())
            .Where(e => e.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (userIds.Count == 0 && extraEmails.Count == 0)
        {
            throw new ValidationException("Choose at least one recipient.");
        }

        foreach (var extra in extraEmails)
        {
            if (!ExtraEmailPattern.IsMatch(extra))
            {
                throw new ValidationException($"“{extra}” is not a valid email address.");
            }
        }

        var selectedUsers = await _db.Users
            .AsNoTracking()
            .Include(u => u.Organizations)
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync(cancellationToken);

        if (selectedUsers.Count != userIds.Count)
        {
            throw new ForbiddenException("One or more selected people were not found in this organization.");
        }

        foreach (var selected in selectedUsers)
        {
            if (!selected.Organizations.Any(m => m.OrganizationId == report.OrganizationId))
            {
                throw new ForbiddenException($"{ReportPersonNames.FromUser(selected)} is not in {report.Organization.Name} and cannot be emailed this report.");
            }
        }

        var recipients = selectedUsers
            .Select(u => u.Email)
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Cast<string>()
            .Concat(extraEmails)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (recipients.Count == 0)
        {
            throw new ValidationException("Choose at least one recipient with an email address.");
        }

        var snapshot = DeserializeSnapshot(report);
        var detail = MapDetail(report);
        var pdf = MaintenanceReportPdf.Build(detail);
        var link = BuildReportLink(report.Id);
        var sentBy = await ReportPersonNames.ForCurrentUserAsync(_db, _currentUser, cancellationToken);
        var subject = $"{report.Organization.Name} — {report.MonthLabel} GIS Maintenance Report";
        var html = BuildEmailHtml(report, snapshot, link, sentBy);
        var text = BuildEmailText(report, snapshot, link, sentBy);
        var attachment = new EmailAttachment(MaintenanceReportPdf.FileName(snapshot), "application/pdf", pdf);

        var result = await _email.SendAsync(
            new OutboundEmail(recipients, subject, html, text, [attachment]),
            cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var recipientList = string.Join("; ", recipients);

        _db.ReportEmailLogs.Add(new ReportEmailLog
        {
            Id = Guid.NewGuid(),
            ReportId = report.Id,
            SentAt = now,
            Recipients = recipientList,
            Subject = subject,
            Mode = result.Mode,
            Delivered = result.Delivered,
            Error = result.Error,
            SentByUserId = _currentUser.UserId
        });

        if (result.Delivered)
        {
            report.LastEmailedAt = now;
            report.LastEmailedTo = recipientList;
            report.EmailCount += 1;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var note = result.Delivered
            ? "Email sent."
            : result.Mode == "dry-run"
                ? "Email is not configured. This was a dry-run — nothing was delivered. A Global Administrator can set SMTP under Admin Settings → Email / SMTP."
                : result.Error ?? "Email was not delivered.";

        return new EmailReportResult(result.Delivered, result.Mode, recipientList, note);
    }

    public async Task<ReportFile> ExportPdfAsync(Guid reportId, CancellationToken cancellationToken = default)
    {
        var report = await GetAsync(reportId, cancellationToken);
        return new ReportFile(MaintenanceReportPdf.Build(report), MaintenanceReportPdf.FileName(report.Snapshot), "application/pdf");
    }

    public async Task<ReportFile> ExportCompletedCsvAsync(Guid reportId, CancellationToken cancellationToken = default)
    {
        var report = await GetAsync(reportId, cancellationToken);
        return MaintenanceReportCsv.Build(report.Snapshot);
    }

    private void EnsureCanMutate()
    {
        if (!_currentUser.CanMutateWorkItems)
        {
            throw new ForbiddenException("Viewers can read reports but cannot generate or email them.");
        }
    }

    private async Task<MonthlyReport> LoadAccessibleAsync(Guid reportId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? _db.MonthlyReports.AsQueryable() : _db.MonthlyReports.AsNoTracking();
        var report = await query
            .Include(r => r.Organization)
            .Include(r => r.GeneratedByUser)
            .Include(r => r.Emails)
            .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);
        if (report is null)
        {
            throw new NotFoundException("Report was not found.");
        }

        await _orgScope.EnsureCanAccessOrganizationAsync(report.OrganizationId, cancellationToken);
        return report;
    }

    private async Task<ReportSnapshot> BuildSnapshotAsync(
        Organization organization,
        string cadence,
        int year,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken)
    {
        var startMs = periodStart.ToUnixTimeMilliseconds();
        var endMs = periodEnd.ToUnixTimeMilliseconds();
        var annual = ReportCadences.IsAnnual(cadence);
        var monthName = annual ? year.ToString(CultureInfo.InvariantCulture) : periodStart.ToString("MMMM", CultureInfo.InvariantCulture);
        var monthLabel = annual ? $"Annual {year}" : periodStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        var periodPhrase = annual ? "this year" : "this month";

        var items = await _db.WorkItems
            .AsNoTracking()
            .Include(w => w.Status)
            .Include(w => w.AssignedToUser)
            .Include(w => w.DocumentType)
            .Include(w => w.TimeEntries)
            .Where(w => w.OrganizationId == organization.Id)
            .ToListAsync(cancellationToken);

        bool InMonth(long? sort) => sort is { } value && value >= startMs && value < endMs;

        var uploaded = items.Count(w => InMonth(w.UploadedAtSort));
        var completedItems = items
            .Where(w =>
                (w.StatusId == SeedIds.StatusWorked || w.StatusId == SeedIds.StatusQcd)
                && InMonth(w.WorkedOnSort))
            .OrderBy(w => w.WorkedOn)
            .ThenBy(w => w.FileName)
            .ToList();
        var pending = items.Count(w => w.StatusId == SeedIds.StatusPending);
        var active = items.Count(w => w.StatusId == SeedIds.StatusInProgress);
        var onHold = items.Count(w => w.StatusId == SeedIds.StatusHeld);

        var monthMinutes = items.SelectMany(w => w.TimeEntries)
            .Where(t => InMonth(t.WorkedOnSort))
            .Sum(t => t.Minutes);

        var typeTotals = new List<ReportNamedCount>
        {
            new("Annexations", completedItems.Sum(w => w.AnnexationCount), "#1890ff"),
            new("Corrections", completedItems.Sum(w => w.CorrectionCount), "#c23b32"),
            new("Plats", completedItems.Sum(w => w.PlatCount), "#52c41a"),
            new("Deeds", completedItems.Sum(w => w.DeedCount), "#722ed1"),
            new("Sketches", completedItems.Count(w => w.IsSketch), "#faad14")
        }.Where(x => x.Count > 0).ToList();

        var rows = completedItems.Select(w => new ReportCompletedItem(
            w.FileName,
            w.UploadedAt,
            w.WorkedOn,
            w.AnnexationCount,
            w.CorrectionCount,
            w.PlatCount,
            w.DeedCount,
            w.IsSketch,
            FormatPropertyIds(w.PropertyIds))).ToList();

        return new ReportSnapshot
        {
            Title = annual ? ReportBranding.AnnualTitle : ReportBranding.Title,
            Cadence = cadence,
            IncludeParcelStatus = !annual,
            OrganizationName = organization.Name,
            MonthName = monthName,
            MonthLabel = monthLabel,
            PeriodPhrase = periodPhrase,
            Intro = annual ? ReportBranding.AnnualIntro(year) : ReportBranding.Intro(monthName),
            Closing = ReportBranding.Closing,
            Contact = new ReportContact(),
            ParcelStatus = annual ? new ReportParcelStatus { Note = string.Empty } : BuildParcelStatus(organization),
            Completed = completedItems.Count,
            MaintenanceByType = typeTotals,
            CompletedItems = rows,
            Uploaded = uploaded,
            Pending = pending,
            Active = active,
            OnHold = onHold,
            Hours = TimeDurations.ToHours(monthMinutes),
            HoursLabel = TimeDurations.Format(monthMinutes)
        };
    }

    private static ReportParcelStatus BuildParcelStatus(Organization organization)
    {
        if (organization.ParcelTotalRealAccounts is not { } total || organization.ParcelWithOwnership is not { } owned)
        {
            return new ReportParcelStatus();
        }

        var missing = Math.Max(total - owned, 0);
        var percent = total == 0 ? 0 : Math.Round(owned * 100m / total, 0);
        return new ReportParcelStatus
        {
            Available = true,
            Note = string.Empty,
            TotalRealAccounts = total,
            ParcelsWithOwnership = owned,
            MissingRealAccounts = missing,
            PercentComplete = percent
        };
    }

    private static string FormatPropertyIds(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(';', value.Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private ReportDetail MapDetail(MonthlyReport report)
    {
        var snapshot = DeserializeSnapshot(report);
        var emails = report.Emails
            .OrderByDescending(e => e.SentAt)
            .Select(e => new ReportEmailLogDto(e.Id, e.SentAt, e.Recipients, e.Mode, e.Delivered, e.Error))
            .ToList();

        if (string.IsNullOrWhiteSpace(snapshot.Cadence))
        {
            snapshot.Cadence = string.IsNullOrWhiteSpace(report.Cadence) ? ReportCadences.Monthly : report.Cadence;
        }

        return new ReportDetail(
            report.Id,
            report.OrganizationId,
            report.Organization.Name,
            string.IsNullOrWhiteSpace(report.Cadence) ? ReportCadences.Monthly : report.Cadence,
            report.Year,
            report.Month,
            report.Version,
            report.MonthLabel,
            report.GeneratedAt,
            ReportPersonNames.FromUser(report.GeneratedByUser),
            report.LastEmailedAt,
            report.LastEmailedTo,
            report.EmailCount,
            report.EmailCount > 0 && report.LastEmailedAt is not null,
            _currentUser.CanMutateWorkItems,
            snapshot,
            emails);
    }

    private static ReportSnapshot DeserializeSnapshot(MonthlyReport report)
    {
        var snapshot = JsonSerializer.Deserialize<ReportSnapshot>(report.SnapshotJson, JsonOptions)
            ?? new ReportSnapshot { MonthLabel = report.MonthLabel, OrganizationName = report.Organization.Name };
        snapshot.Contact.Company = ReportBranding.Company;
        snapshot.Contact.Phone = ReportBranding.Phone;
        snapshot.Contact.Email = ReportBranding.Email;
        snapshot.Contact.Department = ReportBranding.Department;
        snapshot.Contact.Website = ReportBranding.Website;
        return snapshot;
    }

    private string BuildReportLink(Guid reportId)
    {
        var raw = string.IsNullOrWhiteSpace(_emailOptions.PublicBaseUrl)
            ? "http://127.0.0.1:47222"
            : _emailOptions.PublicBaseUrl.TrimEnd('/');
        return $"{raw}/reports/{reportId}";
    }

    private string BuildEmailHtml(MonthlyReport report, ReportSnapshot snapshot, string link, string sentBy)
    {
        var types = snapshot.MaintenanceByType.Count == 0
            ? $"<p>No completed maintenance items {Encode(snapshot.PeriodPhrase)}.</p>"
            : "<ul>" + string.Join("", snapshot.MaintenanceByType.Select(t =>
                $"<li>{Encode(t.Name)} — {t.Count}</li>")) + "</ul>";

        return $"""
            <html><body style="font-family:Segoe UI,sans-serif;color:#262626;">
            <h2 style="color:#1890ff;">{Encode(snapshot.Title)}</h2>
            <p>{Encode(snapshot.OrganizationName)} · {Encode(snapshot.MonthLabel)}</p>
            <p>{Encode(snapshot.Intro)}</p>
            <p>BIS completed <strong>{snapshot.Completed}</strong> maintenance items {Encode(snapshot.PeriodPhrase)}. The GIS Maintenance Report PDF is attached.</p>
            {types}
            <p><a href="{Encode(link)}">Open this report in GIS Dashboard</a> (sign in required).</p>
            <p style="color:#8c8c8c;font-size:12px;">{Encode(snapshot.Contact.Department)} · {Encode(snapshot.Contact.Company)}</p>
            <p style="color:#8c8c8c;font-size:12px;">Sent by {Encode(sentBy)}. Version {report.Version}, generated {report.GeneratedAt:yyyy-MM-dd HH:mm} UTC.</p>
            </body></html>
            """;
    }

    private string BuildEmailText(MonthlyReport report, ReportSnapshot snapshot, string link, string sentBy)
    {
        var types = snapshot.MaintenanceByType.Count == 0
            ? $"No completed maintenance items {snapshot.PeriodPhrase}."
            : string.Join("\n", snapshot.MaintenanceByType.Select(t => $"- {t.Name}: {t.Count}"));

        return $"""
            {snapshot.Title}
            {report.Organization.Name} — {report.MonthLabel}

            {snapshot.Intro}

            Completed maintenance items: {snapshot.Completed}
            {types}

            The GIS Maintenance Report PDF is attached.
            Open this report (sign in required): {link}

            {snapshot.Contact.Department} · {snapshot.Contact.Company}
            {snapshot.Contact.Phone} · {snapshot.Contact.Email}

            Sent by {sentBy}. Version {report.Version}.
            """;
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? "");
}

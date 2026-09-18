using System.Text;
using System.Text.RegularExpressions;
using GisDashboard.Application.Abstractions;
using GisDashboard.Application.Email;
using GisDashboard.Application.Exceptions;
using GisDashboard.Application.WorkItems;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Email;
using GisDashboard.Infrastructure.Persistence;
using GisDashboard.Infrastructure.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GisDashboard.Infrastructure.Services;

public sealed class DashboardReportService : IDashboardReportService
{
    public const string UnconfiguredMessage =
        "Email is not configured. A Global Administrator can set SMTP under Admin Settings → Email / SMTP. Or set Email__Enabled=true and Email__Smtp__Host on the App Service. Prefer a Key Vault reference for Email__Smtp__Password.";

    private static readonly Regex ExtraEmailPattern = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IWorkItemService _workItems;
    private readonly ICurrentUser _currentUser;
    private readonly IOrgScope _orgScope;
    private readonly IEmailSender _email;
    private readonly EmailOptions _emailOptions;
    private readonly AppDbContext _db;

    public DashboardReportService(
        IWorkItemService workItems,
        ICurrentUser currentUser,
        IOrgScope orgScope,
        IEmailSender email,
        IOptions<EmailOptions> emailOptions,
        AppDbContext db)
    {
        _workItems = workItems;
        _currentUser = currentUser;
        _orgScope = orgScope;
        _email = email;
        _emailOptions = emailOptions.Value;
        _db = db;
    }

    public async Task<ExcelExport> ExportPdfAsync(DashboardQuery query, CancellationToken cancellationToken = default)
    {
        var data = await _workItems.GetDashboardAsync(query, cancellationToken);
        var pdf = DashboardPdf.Build(data, query, _currentUser.DisplayName);
        return new ExcelExport(pdf, DashboardPdf.FileName(data), "application/pdf");
    }

    public async Task<IReadOnlyList<DashboardRecipient>> ListRecipientsAsync(
        Guid? organizationId,
        CancellationToken cancellationToken = default)
    {
        EnsureCanSend();
        var allowed = await _orgScope.GetAllowedOrganizationIdsAsync(cancellationToken);
        if (organizationId is { } orgId)
        {
            await _orgScope.EnsureCanAccessOrganizationAsync(orgId, cancellationToken);
            allowed = [orgId];
        }

        var globalRoleId = await _db.Roles.AsNoTracking()
            .Where(r => r.Name == Roles.GlobalAdministrator)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return await _db.Users
            .AsNoTracking()
            .Where(u => u.IsActive && !u.IsArchived && (
                u.Organizations.Any(m => allowed.Contains(m.OrganizationId)) ||
                _db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == globalRoleId)))
            .OrderBy(u => u.DisplayName)
            .Select(u => new DashboardRecipient(u.Id, u.DisplayName, u.Email!))
            .ToListAsync(cancellationToken);
    }

    public async Task<DashboardEmailResult> EmailAsync(
        DashboardQuery query,
        DashboardEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureCanSend();
        if (!_email.IsConfigured)
        {
            throw new ServiceUnavailableException(UnconfiguredMessage);
        }

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

        var allowed = await _orgScope.GetAllowedOrganizationIdsAsync(cancellationToken);
        if (query.OrganizationId is { } orgId)
        {
            await _orgScope.EnsureCanAccessOrganizationAsync(orgId, cancellationToken);
        }

        var selectedUsers = await _db.Users
            .AsNoTracking()
            .Include(u => u.Organizations)
            .Where(u => userIds.Contains(u.Id) && u.IsActive && !u.IsArchived)
            .ToListAsync(cancellationToken);

        if (selectedUsers.Count != userIds.Count)
        {
            throw new ForbiddenException("One or more selected people were not found.");
        }

        var globalRoleId = await _db.Roles.AsNoTracking()
            .Where(r => r.Name == Roles.GlobalAdministrator)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var globalUserIds = await _db.UserRoles.AsNoTracking()
            .Where(ur => ur.RoleId == globalRoleId)
            .Select(ur => ur.UserId)
            .ToListAsync(cancellationToken);

        foreach (var selected in selectedUsers)
        {
            var inScope = globalUserIds.Contains(selected.Id)
                || selected.Organizations.Any(m => allowed.Contains(m.OrganizationId));
            if (!inScope)
            {
                throw new ForbiddenException($"{selected.DisplayName} is outside your organization scope.");
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

        var data = await _workItems.GetDashboardAsync(query, cancellationToken);
        var pdf = DashboardPdf.Build(data, query, _currentUser.DisplayName);
        var fileName = DashboardPdf.FileName(data);
        var link = DashboardLink(query);
        var subject = $"GIS Dashboard — {data.RangeLabel}";
        var html = BuildHtml(data, link);
        var text = BuildText(data, link);

        var result = await _email.SendAsync(
            new OutboundEmail(
                recipients,
                subject,
                html,
                text,
                [new EmailAttachment(fileName, "application/pdf", pdf)]),
            cancellationToken);

        if (!result.Delivered)
        {
            throw new ServiceUnavailableException(result.Error ?? UnconfiguredMessage);
        }

        return new DashboardEmailResult(true, result.Mode, string.Join("; ", recipients), "Dashboard PDF sent.");
    }

    private void EnsureCanSend()
    {
        if (!_currentUser.CanMutateWorkItems)
        {
            throw new ForbiddenException("Your role cannot email the dashboard.");
        }
    }

    private string DashboardLink(DashboardQuery query)
    {
        var origin = string.IsNullOrWhiteSpace(_emailOptions.PublicBaseUrl)
            ? "https://appgisdashboard-hffvekhnb5bybxb3.southcentralus-01.azurewebsites.net"
            : _emailOptions.PublicBaseUrl.TrimEnd('/');
        var qs = new List<string>();
        if (query.OrganizationId is { } org) qs.Add($"organizationId={org:D}");
        if (query.StatusId is { } status) qs.Add($"statusId={status:D}");
        if (query.AssignedToUserId is { } assigned) qs.Add($"assignedToUserId={assigned:D}");
        if (query.From is { } from) qs.Add($"from={Uri.EscapeDataString(from.ToString("o"))}");
        if (query.To is { } to) qs.Add($"to={Uri.EscapeDataString(to.ToString("o"))}");
        return qs.Count == 0 ? $"{origin}/" : $"{origin}/?{string.Join("&", qs)}";
    }

    private static string BuildHtml(DashboardResponse data, string link)
    {
        var kpis = string.Join(" · ", data.Kpis.Select(k => $"{WebUtilityEncode(k.Label)} {k.Count}"));
        return $"""
            <p>GIS Dashboard for <strong>{WebUtilityEncode(data.RangeLabel)}</strong>.</p>
            <p>{kpis}</p>
            <p>The current dashboard view is attached as a PDF. Open the live page: <a href="{link}">GIS Dashboard</a>.</p>
            <p>BIS Consultants</p>
            """;
    }

    private static string BuildText(DashboardResponse data, string link)
    {
        var body = new StringBuilder();
        body.AppendLine($"GIS Dashboard — {data.RangeLabel}");
        foreach (var kpi in data.Kpis)
        {
            body.AppendLine($"{kpi.Label}: {kpi.Count}");
        }

        body.AppendLine();
        body.AppendLine("The current dashboard view is attached as a PDF.");
        body.AppendLine(link);
        return body.ToString();
    }

    private static string WebUtilityEncode(string value) =>
        System.Net.WebUtility.HtmlEncode(value);
}

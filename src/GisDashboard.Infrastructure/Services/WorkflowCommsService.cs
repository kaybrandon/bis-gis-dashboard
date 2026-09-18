using System.Net;
using System.Text;
using GisDashboard.Application.Email;
using GisDashboard.Application.WorkItems;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Email;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GisDashboard.Infrastructure.Services;

public sealed class WorkflowCommsService : IWorkflowComms
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _email;
    private readonly EmailOptions _options;
    private readonly ILogger<WorkflowCommsService> _logger;

    public WorkflowCommsService(
        AppDbContext db,
        IEmailSender email,
        IOptions<EmailOptions> options,
        ILogger<WorkflowCommsService> logger)
    {
        _db = db;
        _email = email;
        _options = options.Value;
        _logger = logger;
    }

    public async Task NotifyStatusChangedAsync(
        WorkItem item,
        string oldStatusName,
        string newStatusName,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(oldStatusName, newStatusName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var recipients = await StatusChangeRecipientsAsync(item, cancellationToken);
        if (recipients.Count == 0)
        {
            _logger.LogInformation(
                "Status-change email skipped (no recipients). WorkItem={Id} {Old} → {New}",
                item.Id,
                oldStatusName,
                newStatusName);
            return;
        }

        var orgName = await OrganizationNameAsync(item, cancellationToken);
        var title = WorkItemTitle(item);
        var subject = $"[{orgName}] {title}: {oldStatusName} → {newStatusName}";
        var link = WorkItemLink(item.Id);
        var text = new StringBuilder()
            .AppendLine($"{orgName} — {title}")
            .AppendLine($"Status changed from {oldStatusName} to {newStatusName}.")
            .AppendLine()
            .AppendLine(link)
            .ToString();
        var html = $"""
            <p><strong>{WebUtility.HtmlEncode(orgName)}</strong> — {WebUtility.HtmlEncode(title)}</p>
            <p>Status changed from <strong>{WebUtility.HtmlEncode(oldStatusName)}</strong> to <strong>{WebUtility.HtmlEncode(newStatusName)}</strong>.</p>
            <p><a href="{WebUtility.HtmlEncode(link)}">Open work item</a></p>
            """;

        await SendAsync(recipients, subject, html, text, cancellationToken);
    }

    public async Task NotifyClientCommentAsync(
        WorkItem item,
        string authorName,
        string body,
        Guid authorUserId,
        CancellationToken cancellationToken = default)
    {
        var recipients = await StaffRecipientsAsync(item, authorUserId, cancellationToken);
        if (recipients.Count == 0)
        {
            _logger.LogInformation("Client-comment email skipped (no recipients). WorkItem={Id}", item.Id);
            return;
        }

        var orgName = await OrganizationNameAsync(item, cancellationToken);
        var title = WorkItemTitle(item);
        var subject = $"[{orgName}] New comment on {title}";
        var link = WorkItemLink(item.Id);
        var excerpt = body.Length > 400 ? body[..400] + "…" : body;
        var text = new StringBuilder()
            .AppendLine($"{authorName} posted a client-visible comment on {title}.")
            .AppendLine()
            .AppendLine(excerpt)
            .AppendLine()
            .AppendLine(link)
            .ToString();
        var html = $"""
            <p><strong>{WebUtility.HtmlEncode(authorName)}</strong> posted a client-visible comment on <strong>{WebUtility.HtmlEncode(title)}</strong>.</p>
            <blockquote>{WebUtility.HtmlEncode(excerpt)}</blockquote>
            <p><a href="{WebUtility.HtmlEncode(link)}">Open work item</a></p>
            """;

        await SendAsync(recipients, subject, html, text, cancellationToken);
    }

    public async Task NotifyPublicUploadReceivedAsync(
        Organization organization,
        int fileCount,
        IReadOnlyList<string> fileNames,
        CancellationToken cancellationToken = default)
    {
        var count = Math.Max(fileCount, fileNames.Count);
        if (count <= 0)
        {
            return;
        }

        var recipients = await AssignedTechEmailsAsync(organization.Id, excludeUserId: null, cancellationToken);
        if (recipients.Count == 0)
        {
            _logger.LogInformation("Public-upload email skipped (no assigned techs). Org={Org}", organization.Id);
            return;
        }

        var names = fileNames.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Take(20).ToList();
        var subject = $"[{organization.Name}] {count} file{(count == 1 ? "" : "s")} received";
        var listText = names.Count == 0
            ? ""
            : Environment.NewLine + string.Join(Environment.NewLine, names.Select(x => $"• {x}"));
        var extra = count > names.Count && names.Count > 0 ? $"{Environment.NewLine}…and {count - names.Count} more." : "";
        var text = $"{count} file{(count == 1 ? "" : "s")} uploaded to {organization.Name}.{listText}{extra}";
        var htmlItems = names.Count == 0
            ? ""
            : "<ul>" + string.Join("", names.Select(x => $"<li>{WebUtility.HtmlEncode(x)}</li>")) + "</ul>";
        var html = $"<p>{count} file{(count == 1 ? "" : "s")} uploaded to <strong>{WebUtility.HtmlEncode(organization.Name)}</strong>.</p>{htmlItems}";

        await SendAsync(recipients, subject, html, text, cancellationToken);
    }

    private async Task SendAsync(
        IReadOnlyList<string> recipients,
        string subject,
        string html,
        string text,
        CancellationToken cancellationToken)
    {
        var result = await _email.SendAsync(
            new OutboundEmail(recipients, subject, html, text, []),
            cancellationToken);
        if (!result.Delivered)
        {
            _logger.LogInformation(
                "Workflow email failed closed. Mode={Mode} Error={Error} To={To} Subject={Subject}",
                result.Mode,
                result.Error,
                string.Join(", ", recipients),
                subject);
        }
    }

    private async Task<IReadOnlyList<string>> StatusChangeRecipientsAsync(WorkItem item, CancellationToken cancellationToken)
    {
        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var email in await ClientContactEmailsAsync(item.OrganizationId, cancellationToken))
        {
            emails.Add(email);
        }

        foreach (var email in await AssignedTechEmailsAsync(item.OrganizationId, null, cancellationToken))
        {
            emails.Add(email);
        }

        if (item.AssignedToUserId is { } assignee)
        {
            var email = await UserEmailAsync(assignee, cancellationToken);
            if (email is not null)
            {
                emails.Add(email);
            }
        }

        return emails.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task<IReadOnlyList<string>> StaffRecipientsAsync(WorkItem item, Guid? excludeUserId, CancellationToken cancellationToken)
    {
        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var email in await AssignedTechEmailsAsync(item.OrganizationId, excludeUserId, cancellationToken))
        {
            emails.Add(email);
        }

        if (item.AssignedToUserId is { } assignee && assignee != excludeUserId)
        {
            var email = await UserEmailAsync(assignee, cancellationToken);
            if (email is not null)
            {
                emails.Add(email);
            }
        }

        return emails.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task<IReadOnlyList<string>> ClientContactEmailsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var viewerRole = Roles.Viewer;
        var uploaderRole = Roles.Uploader;
        var adminRole = Roles.Administrator;
        var rows = await (
            from member in _db.UserOrganizations.AsNoTracking()
            join user in _db.Users.AsNoTracking() on member.UserId equals user.Id
            join ur in _db.UserRoles on user.Id equals ur.UserId
            join role in _db.Roles on ur.RoleId equals role.Id
            where member.OrganizationId == organizationId
                  && user.IsActive
                  && !user.IsArchived
                  && user.Id != SeedIds.TokenUploadUser
                  && user.Email != null
                  && user.Email != ""
                  && (role.Name == viewerRole || role.Name == uploaderRole || role.Name == adminRole)
            select user.Email
        ).ToListAsync(cancellationToken);

        return rows
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyList<string>> AssignedTechEmailsAsync(
        Guid organizationId,
        Guid? excludeUserId,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from tech in _db.OrganizationTechs.AsNoTracking()
            join user in _db.Users.AsNoTracking() on tech.UserId equals user.Id
            where tech.OrganizationId == organizationId
                  && user.IsActive
                  && !user.IsArchived
                  && user.Id != SeedIds.TokenUploadUser
                  && (excludeUserId == null || user.Id != excludeUserId)
                  && user.Email != null
                  && user.Email != ""
            select user.Email
        ).ToListAsync(cancellationToken);

        return rows
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<string?> UserEmailAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == SeedIds.TokenUploadUser)
        {
            return null;
        }

        var email = await _db.Users.AsNoTracking()
            .Where(x => x.Id == userId && x.IsActive && !x.IsArchived && x.Email != null && x.Email != "")
            .Select(x => x.Email)
            .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim();
    }

    private async Task<string> OrganizationNameAsync(WorkItem item, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(item.Organization?.Name))
        {
            return item.Organization.Name;
        }

        return await _db.Organizations.AsNoTracking()
            .Where(x => x.Id == item.OrganizationId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Organization";
    }

    private static string WorkItemTitle(WorkItem item) =>
        string.IsNullOrWhiteSpace(item.Title) ? item.FileName : item.Title;

    private string WorkItemLink(Guid workItemId)
    {
        var root = string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            ? "http://127.0.0.1:47222"
            : _options.PublicBaseUrl.TrimEnd('/');
        return $"{root}/documents/{workItemId:D}";
    }
}

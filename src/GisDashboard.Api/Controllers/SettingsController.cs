using GisDashboard.Application.Company;
using GisDashboard.Application.Connections;
using GisDashboard.Application.WorkItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GisDashboard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/settings")]
public sealed class SettingsController : ControllerBase
{
    private readonly UploadOptions _uploads;
    private readonly ICompanyContactService _company;
    private readonly IConnectionService _connections;

    public SettingsController(
        IOptions<UploadOptions> uploads,
        ICompanyContactService company,
        IConnectionService connections)
    {
        _uploads = uploads.Value;
        _company = company;
        _connections = connections;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var company = await _company.GetAsync(cancellationToken);
        return Ok(new
        {
            appName = "GIS Dashboard",
            phase = "Phase 3.1",
            uploads = new
            {
                maxFileBytes = _uploads.EffectiveMaxFileBytes,
                maxFileMegabytes = _uploads.EffectiveMaxFileMegabytes,
                concurrency = _uploads.EffectiveConcurrency,
                note = "Per-file limit (default 50 MB). Change on the App Service with Uploads__MaxFileMegabytes (25–50 typical) or Uploads__MaxFileBytes. The browser queues 3 files at a time."
            },
            company,
            features = new
            {
                timeLogging = new { enabled = true, note = "Hours on work items — Viewer read, Editor and Administrators write. Floating clock start/stop or add time without leaving the current screen." },
                floatingTimeClock = new { enabled = true, note = "Always-available clock for BIS technicians. Time is logged on a GIS work item (current document, or pick one) — same entries as the work-item time log and time report cards." },
                comments = new { enabled = true, note = "Comments are client-visible. A new comment emails the assignee and Assigned tech(s). Internal Notes stay staff-only." },
                uploadClientNotes = new { enabled = true, note = "Optional client notes on Upload Documents and the public upload link become a client-visible comment on each file in the batch. Assigned technician is shown as first name plus last initial (primary Assigned tech when set)." },
                statusChangeEmail = new { enabled = true, note = "Status changes email org client contacts (when known), Assigned tech(s), and the assignee. Fail closed if SMTP is off — the status still saves." },
                myQueue = new { enabled = true, note = "Manage Documents presets: Assigned to me, Priority, Due this week, and Clear. Stored in this browser." },
                priorityNeededBy = new { enabled = true, note = "Priority work can have a needed-by date. Overdue items are marked. Due this week is a My queue filter. The date is included on priority alerts." },
                mentions = new { enabled = true, note = "@username in a comment notifies that person in the bell." },
                dashboard = new { enabled = true, note = "KPI cards, scoped filters, date range, volume / hours charts, PDF download, and email of the current view" },
                dashboardExport = new { enabled = true, note = "Dashboard Download PDF and Send report use the current organization, status, assignee, and date range. Send requires SMTP (Admin Settings → Email / SMTP, or Email__Enabled and Email__Smtp__Host) and confirms before sending." },
                excelExport = new { enabled = true, note = "Manage Documents export (current filters)" },
                sketchFlag = new { enabled = true, note = "Sketch? flag is the hook — DeedAi/sketch linking is a later v1 phase" },
                persistedPdfMarkup = new { enabled = false, note = "Later v1 phase — ephemeral highlights only for now" },
                entraSso = new { enabled = false, note = "Later v1 phase" },
                deedAiLinking = new { enabled = false, note = "Later v1 phase" },
                shapefileZip = new { enabled = false, note = "Later v1 phase" },
                reports = new { enabled = true, note = "GIS Maintenance Report monthly or annual: parcel status (monthly), completed totals by type, completed-item table, PDF/CSV, history, and email" },
                timeReportCards = new { enabled = true, note = "Hours on GIS work items by person, period, and client. BIS staff always see their own (Global Administrator sees the team). Clients see cards only when a Global Administrator turns that organization on." },
                priorityWork = new { enabled = true, note = "Priority on a work item or upload notifies organization assigned tech(s), the work-item Assigned To when set, and every Global Administrator. A Global Admin who flags the item still sees it in the bell. Needed-by date is included when set. No email ingest." },
                retention = new { enabled = false, note = "Later v1 phase" },
                tokenizedUpload = new { enabled = true, note = "Per-organization no-login upload URL — upload one or many PDFs (queued, same per-file size limit as signed-in upload)" },
                massUpload = new { enabled = true, note = "Upload Documents and the public upload link accept multiple PDFs. Each file becomes its own work item. The queue runs 3 at a time. Oversized files are skipped; the rest of the batch continues." },
                uploadDocuments = new { enabled = true, note = "Left-nav Upload Documents page. Manage Documents + Upload redirects there. Viewers upload to their organization; staff pick the client and later set type and priority." },
                viewerUpload = new { enabled = true, note = "Viewer can upload documents for their assigned organization. They cannot change status, assignment, type, or priority." },
                staffAllOrganizations = new { enabled = true, note = "Global Administrator, Administrator, and Editor see and work documents from every organization. Viewer stays org-scoped." },
                reviewed = new { enabled = true, note = "Reviewed checkbox next to Priority on the work item. Manage Documents shows Review yes/no instead of Hours." },
                environmentStatus = new { enabled = true, note = "Status page (Global Administrator only). Live checks from this app (API, database, storage, Key Vault when configured). App Insights, email, and Azure OpenAI are configuration-only." },
                companyContact = new { enabled = true, note = "BIS Consultants phone, email, address, and website. A Global Administrator edits them on Admin Settings. The same block is shown on Upload Documents and the public upload link." },
                emailSmtp = new { enabled = true, note = "Admin Settings → Email / SMTP. Host, port, TLS, From, username, write-only password, optional reply-to. Save and Send test email. Password is never returned to the browser. Forgot password on the login screen uses the same SMTP and fail-closes until it is configured." },
                forgotPassword = new { enabled = true, note = "Login → Forgot password. Always returns a generic message. The reset email is sent only when SMTP is configured and an account exists. Token expires in 30 minutes. Rate-limited." },
                aiFillFromPdf = new { enabled = true, note = "Work-item AI fill from PDF text (Title, Type, Property IDs, counts, Worked date). Never writes Status, Assignee, or flags. Amber until Approve or edit; Save is still required. Fail closed unless AzureOpenAI__Endpoint and AzureOpenAI__ApiKey are set (Key Vault for the key). Deployment gpt-4.1-mini on oai-bis-deed-ai." },
                autoSave = new { enabled = false, note = "Later v1 phase" },
                presence = new { enabled = true, note = "Signed-in staff (Global Administrator, Administrator, Editor) see who is online, the page or work item they are on, and a Clocked in badge when the floating clock is running. The list is in the header and on Dashboard; Global Administrators also see it on Status. Viewers do not see other organizations’ presence. Offline after about three minutes without a heartbeat." },
                connections = new { enabled = true, note = "Admin Connections: Source / Destination / Direction. Azure paths display and persist as workfiles/orgs/… (container + folder) in resource group rg-bis-gis-dashboard. Bare /orgs/… is the same Azure folder. Check folders shows Pass or Fail. Sync errors show on the list and Edit." },
                clientShell = new { enabled = false, note = "Later v1 phase" }
            }
        });
    }

    [HttpGet("file-kinds")]
    public Task<FileKindsResponse> FileKinds(CancellationToken cancellationToken) =>
        _connections.GetFileKindsAsync(cancellationToken);

    [HttpGet("ftp")]
    public IActionResult FtpSettings() => Ok(new { nightlyHourUtc = 7 });
}

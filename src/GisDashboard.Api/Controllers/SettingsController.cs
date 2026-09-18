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
                acceptedExtensions = UploadFileTypes.Extensions,
                supportedTypesLabel = UploadFileTypes.SupportedTypesLabel,
                accept = UploadFileTypes.AcceptAttribute,
                note = "Per-file limit (default 50 MB). PDF, Word (.doc, .docx), Excel (.xls, .xlsx), or images. Change on the App Service with Uploads__MaxFileMegabytes (25–50 typical) or Uploads__MaxFileBytes. The browser queues 3 files at a time."
            },
            company,
            features = new
            {
                timeLogging = new { enabled = true, note = "Hours on work items — Viewer read, Editor and Administrators write. Add or edit entries on the work-item Time log. Manage Documents Total time is the sum of those entries." },
                floatingTimeClock = new { enabled = true, note = "Sitewide staff attendance clock-in / clock-out on every signed-in GIS screen. Collapsible control with a Clock in / Clock out label. Presence only — not a work-item or document timer, and not punch-to-payroll." },
                comments = new { enabled = true, note = "Comments are client-visible. Uploaders and staff can post. Viewers can read but not post. A new comment emails the assignee and Assigned tech(s). Internal Notes stay staff-only." },
                uploadClientNotes = new { enabled = true, note = "Optional client notes on Upload Documents and the public upload link become a client-visible comment on each file in the batch. Assigned technician is the org default (QC01/CR09), shown as first name plus last initial (primary Assigned tech when set)." },
                statusChangeEmail = new { enabled = true, note = "Status changes email org client contacts (when known), Assigned tech(s), and the assignee. Fail closed if SMTP is off — the status still saves." },
                myQueue = new { enabled = true, note = "CR05 — Staff Manage Documents defaults to assigned to the signed-in user, Pending first then other statuses, oldest upload/created ascending. Saved My queue presets (Assigned to me, Unassigned, Priority, Due this week, Clear) may override after the user sets them. Queues use work-item Assigned to (CR09), not org Assigned technician. Viewer/Uploader have no Assignee dropdown (QC08)." },
                priorityNeededBy = new { enabled = true, note = "Priority work can have a needed-by date. Overdue items are marked. Due this week is a My queue filter. The date is included on priority alerts." },
                mentions = new { enabled = true, note = "@username in a comment notifies that person in the bell." },
                dashboard = new { enabled = true, note = "KPI cards, scoped filters, date range, volume / hours charts, document counts by CAD and technician, PDF download, and email of the current view. CR03 — Active tile count is the full matching Active total (not one page) for the current organization and assignee scope. CR05 — staff default to the signed-in assignee; Active opens that same queue. CR10 — Documents by CAD (v1 = Organization name) and Documents by technician (Assigned to, including Unassigned) use created/uploaded dates and match the filtered list." },
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
                tokenizedUpload = new { enabled = true, note = "Per-organization no-login upload URL — upload one or many PDF, Word, Excel, or image files (queued, same per-file size limit as signed-in upload)" },
                massUpload = new { enabled = true, note = "Upload Documents and the public upload link accept multiple PDF, Word (.doc, .docx), Excel (.xls, .xlsx), or image files. Each file becomes its own work item. The queue runs 3 at a time. Oversized files are skipped; the rest of the batch continues." },
                uploadDocuments = new { enabled = true, note = "Left-nav Upload Documents page (/upload-documents). Manage Documents + Upload redirects there. Uploaders upload to assigned organization(s); staff pick the client. CR08 — new files auto-assign Assigned to the org primary Assigned technician, else the first Assigned tech, else stay unassigned and visible to staff. Viewers are read-only." },
                documentAssignment = new { enabled = true, note = "CR08 — New uploads auto-assign Assigned to the organization’s primary Assigned technician if set, else the first Assigned tech, else leave unassigned. Unassigned work is visible to authorized staff (Unassigned queue / Assigned to filter) and can be assigned. QC01 org-tech edits do not rewrite existing document Assigned to." },
                assignmentFields = new { enabled = true, note = "CR09 — Assigned technician / Assigned tech(s) is the organization default (QC01). Assigned to is the work-item assignee used by queues and reports. The two meanings stay separate." },
                viewerUpload = new { enabled = false, note = "QC04 — Viewer is upload/comment-free. Use Uploader for client uploads and client-visible comments on assigned organizations." },
                uploaderRole = new { enabled = true, note = "QC04 — Uploader sees Viewer-accessible pages, uploads to assigned org(s) only, and posts client-visible Comments. No Internal Notes, doc editing, tech assignment, or user management. Dashboard Assignee is hidden (QC08)." },
                dashboardAssignee = new { enabled = true, note = "QC08 — Dashboard Assignee is hidden for Viewer and Uploader. Dashboard choices, charts, counts, and document results stay assigned-org only. Filter params and direct API cannot open another org. Editors and Administrators keep authorized cross-org filtering (QC03). CR05 — staff default to the signed-in assignee and may switch to all, Unassigned, or another person; Viewer/Uploader never get a cross-org Assignee control. CR09 — this filter is work-item Assigned to, not org Assigned technician. CR10 — Viewer/Uploader document volume by CAD and technician is assigned-org totals only." },
                documentVolume = new { enabled = true, note = "CR10 — Dashboard document counts by CAD and by technician (not only Hours). v1 CAD is Organization (client) name. Technician is document Assigned to, including an Unassigned bucket. Date is created/uploaded in the selected range. Chart totals match the filtered Manage Documents list. Chart click opens that list. QC08 — Viewer/Uploader see assigned-org totals only." },
                staffAllOrganizations = new { enabled = true, note = "Global Administrator, Administrator, and Editor see and work documents from every organization. Uploader and Viewer stay org-scoped." },
                wishList = new { enabled = true, note = "WL01–WL13 verify/close. WL01 staff all orgs (QC03). WL02 client upload into assigned org (QC04). WL03 separate Upload Documents multi-file (QC04/QC05). WL04 org auto on client upload; staff set type/priority. WL05 staff select org; batch stays that org. WL06 left-nav + Manage Documents + Upload open /upload-documents. WL07 Needs Review status vocabulary held until CR11 merges. WL08 Deed, Plat, Survey, Subdivision, Other. WL09 Assigned to auto org Assigned technician (QC01/CR08/CR09). WL10 Reviewed next to Priority. WL11 Review Yes/No on Manage Documents. WL12 TIFF first-page + image preview (CR04). WL13 Manage Documents defaults to signed-in Assigned to (CR05/QC08)." },
                reviewed = new { enabled = true, note = "Reviewed checkbox next to Priority on the work item. Independent of document status, including Needs Review — changing status does not change Reviewed. Manage Documents keeps a Review yes/no column and a separate Total time (hours) column. Total time is the sum of time entries on that document." },
                needsReviewStatus = new { enabled = true, note = "Needs Review is a document status in detail and grid selectors and in every Editor status filter. Distinct from Reviewed Yes/No. No separate review-owner, routing, or notify queue." },
                statusVocabulary = new { enabled = true, note = "CR11 — Selectors, filters, tiles, charts, and exports use Active, Pending, Complete, On-Hold, Cancelled, Needs Review. In Progress/Held/Worked keep the same IDs (approved display map). QC'd is not remapped until Brandon maps chart stages; charts show canonical statuses only. Needs Review stays a workflow status, not Reviewed Yes/No." },
                environmentStatus = new { enabled = true, note = "Status page (Global Administrator only). Live checks from this app (API, database, storage, Key Vault when configured). App Insights, email, and Azure OpenAI are configuration-only." },
                companyContact = new { enabled = true, note = "BIS Consultants phone, email, address, and website. A Global Administrator edits them on Admin Settings. The same block is shown on Upload Documents and the public upload link." },
                emailSmtp = new { enabled = true, note = "Admin Settings → Email / SMTP. Host, port, TLS, From, username, write-only password, optional reply-to. Save and Send test email. Password is never returned to the browser. Forgot password on the login screen uses the same SMTP and fail-closes until it is configured." },
                forgotPassword = new { enabled = true, note = "Login → Forgot password. Always returns a generic message. The reset email is sent only when SMTP is configured and an account exists. Token expires in 30 minutes. Rate-limited." },
                aiFillFromPdf = new { enabled = true, note = "Work-item AI fill from PDF text (Title, Type, Property IDs, counts, Worked date). Never writes Status, Assignee, or flags. Amber until Approve or edit; Save is still required. Fail closed unless AzureOpenAI__Endpoint and AzureOpenAI__ApiKey are set (Key Vault for the key). Deployment gpt-4.1-mini on oai-bis-deed-ai." },
                autoSave = new { enabled = false, note = "Later v1 phase" },
                headerGreeting = new { enabled = true, note = "App header is Good morning / Good afternoon / Good evening, {FirstName} (America/Chicago; first token of Full name) plus a same-line quoted rotating morale tagline. Org title chrome does not replace the greeting." },
                presence = new { enabled = true, note = "Signed-in staff (Global Administrator, Administrator, Editor) see who is online, the page or work item they are on, and a Clocked in badge when the staff attendance clock is running. The list is a collapsible panel on the left sider immediately under Status (not a header / menu-bar strip). Need help? raises a yellow hand on that panel. Light messages are online-only. A header envelope lists waiting Need-help threads (including offline senders) with an unread badge; opening a thread uses the same light sheet and marks it read. Global Administrators also see it on Status. Viewers do not see other organizations’ presence. Offline after about three minutes without a heartbeat." },
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

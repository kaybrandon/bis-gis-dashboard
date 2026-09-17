# BIS GIS Dashboard

Standalone Azure web app for BIS Consultants GIS work-item tracking. Live app: [https://appgisdashboard-hffvekhnb5bybxb3.southcentralus-01.azurewebsites.net](https://appgisdashboard-hffvekhnb5bybxb3.southcentralus-01.azurewebsites.net). Historical cloud-agent work lived on Cursor Origin `brandon-kay/tmp-db0abcf58e9ec23f`; this GitHub repo is a visibility mirror.

Client organizations upload plats, surveys, deeds, and related files. Editors assign, work, QC, and keep one shared **Internal Notes** field. This app is independent of BIS Admin, FTP, and COLO at runtime.

Phase 1 covers auth/roles/orgs, Manage Documents (server-side search/filter/sort/page + DateTimeOffset), Blob/local file upload, and the split PDF/image viewer with filtered next/prev.

Phase 2 adds **time logging** on work items, first-class **Internal Notes** (with lightweight history of the same field), and the locked **two-axis role model**: one Global Administrator plus organization-scoped Administrator / Editor / Viewer. There is no Client *role* — a client is an organization.

The Dashboard date range (default last 30 days) filters KPIs and charts. **Download PDF** and **Send report** use the same organization / status / assignee / date filters. Send asks for confirmation and fails closed when email is not configured.

Phase 3 adds **dashboard KPIs** (Active, Pending, Completed in the selected date range), Manage Documents **status buckets** (All Pending, My Work Items, On Hold, Completed, First/Final Deadline), Excel export, group-by, and work-item **detail parity**: Active / On Hold / Complete, Assigned To (name only), Split? / Sketch? flags, worked date, hours from time logs, annexation/correction/deed/plat counters, multi-line Property IDs, Undo + Save, and a **comments** thread beside Internal Notes. Viewer highlights stay ephemeral.

Phone layout (~390px) keeps the desktop grid dense and switches the app shell, Dashboard, and Manage Documents to stacked filters, compact KPI cards, and a work-item card list so the page does not scroll sideways. Organization, Users, and work-item detail use the same overflow rules.

**AI fill from PDF** on the work-item form reads extractable PDF text (no OCR / Document Intelligence) and drafts Title, Type, Property IDs, annexation/correction/deed/plat counts, and Worked date when the text supports them. Status, assignee, Split/Sketch/Priority, and Reviewed are never set by AI. Each filled field shows a confidence percent and stays amber until Approve or an edit. Overall confidence sits on the action bar. **Save is still required** — AI fill never persists on its own. Azure OpenAI `gpt-4.1-mini` (prefer resource `oai-bis-deed-ai`) is required; the API fails closed if `AzureOpenAI__Endpoint` or `AzureOpenAI__ApiKey` is missing.

Phase 3.1 adds **dashboard charts** (status volume, uploads vs completions over 30 days, hours by assignee and by client), **edit** for Organizations and Users (not only Add), a **per-organization tokenized upload URL** so someone can upload files into that client without signing in, **per-client GIS Maintenance Reports** in **monthly** or **annual** cadence (parcel status on monthly; year totals + multi-page completed items on annual; PDF/CSV, versioned history, and email), **technician time report cards** (hours already logged on GIS work items, summarized by person, period, and client), a **floating time clock** so technicians can start/stop or add hours on a GIS work item from any signed-in screen, and **in-app priority work** so a client can flag an item (or the upload link) instead of emailing a technician. Assigned technicians see a Priority badge and bucket, a Dashboard KPI, and a header notification (polled). Regenerating an upload token invalidates the previous link. The URL is for uploads only — not booking. KPI cards and chart segments are clickable — they open Manage Documents with the matching bucket, status, assignee, client, or day filter. On Organizations, the client name (or row) opens that client's details; each row has a **Reports** button to GIS Maintenance Reports filtered to that `organizationId`.

**Upload Documents** is its own left-nav page (`/upload-documents`). Manage Documents **+ Upload** redirects there. Viewers upload to their organization (type and priority are set later by BIS staff). Global Administrator, Administrator, and Editor pick the client; new items auto-assign to that organization’s BIS editor (Assigned tech). Both the signed-in form and the public `/upload/{token}` page show the Assigned technician as **FirstName L.** (primary Assigned tech when set) and accept optional **Client notes**, which become a client-visible comment on each file in the batch. Document types are **Deed, Plat, Survey, Subdivision, Other**. Status is a dropdown: **Active, Pending, Complete, On-Hold, Cancelled**. Manage Documents shows **Review** yes/no instead of Hours; the work-item form has **Reviewed** next to Priority.

**Mass upload:** Upload Documents and the public `/upload/{token}` page accept **multiple PDFs** (multi-select and drag-drop, up to 200 files). Each successful file becomes its own work item. Document type, assignment, and priority note apply to the whole batch when the signed-in user can set them. The browser queues **3 files at a time**. A sticky bar shows **Uploading N of M**, a percent, running tallies (done / failed / skipped / remaining), and **Keep this window open until finished.** Closing the tab while files are still uploading prompts a browser warning. When the batch finishes, a dismissible summary stays on the page with the per-file list (completed rows can be collapsed). The default **per-file limit is 50 MB** (`Uploads:MaxFileBytes` = 52,428,800). A file over the limit is skipped with a clear message; the rest of the batch continues.

**Forgot password** on the login screen asks for email or username and always shows **If an account exists, we sent a reset link** (no user enumeration). The email has a 30-minute, single-use link to choose a new password. SMTP must be configured (Admin Settings → Email / SMTP). If it is not, the page says **Password reset isn’t available yet — contact your admin.** Requests are rate-limited.

A Global Administrator edits **BIS Consultants** contact (phone, email, address, website) on **Admin Settings**. Defaults are `800-247-9045`, `gissupport@bisconsultants.com`, `14802 Venture Dr. Farmers Branch Tx 75234`, and `www.bisconsultants.com`. The same block is shown on **Upload Documents** and the public `/upload/{token}` page. Azure environment checks live on the **Status** page (`/status`), not on Admin Settings.

A small **Powered By: BIS Consultants** footer (link to https://www.bisconsultants.com, new tab) sits on the signed-in shell, the login page, and the public upload page.

Styling uses Mask F / Mockitt Admin tokens (same system as DeedAi): primary `#1890ff`, sider `#001529`, layout `#f0f2f5`, 2px radius, Ant Design compact algorithm. Short fields stay in a readable max-width or two-column grid — they do not stretch across the viewport. Filter toolbars use fixed-width controls that stack to full width on phones (~390px). The desktop left menu collapses to icons (preference in `localStorage`). Section help sits in a **?** tooltip next to the title, not a gray paragraph under it. The account menu is **View Profile** and **Sign Out**. Profile is an editable page (username, full name, email, work phone, photo, change password). Users admin uses the same username / full name / work phone fields. Header shows Full name if set, otherwise Username. Work-item detail can **Pop out** the viewer or the form into a new window (`?layout=viewer|form&popout=1`). Layout language matches the current GIS dashboard — KPIs on top, filters and buckets left, grid below, split viewer.

## Stack

- ASP.NET Core / .NET 10 Web API (Windows App Service zipdeploy-friendly)
- React + Vite SPA (Ant Design tokens + Ant Design Plots)
- EF Core with SQLite locally and Azure SQL in Azure
- Azure Blob (`stbisgisdashboard` / `workfiles`) with local filesystem fallback
- Azure Key Vault configuration when `KeyVaultUri` or `KeyVault__VaultUri` is set
- Application Insights when the connection string is present
- Email/password auth. Sign-in accepts **username or email** (case-insensitive). Entra SSO is a later v1 phase, not a cut.

## Local run

Requires .NET 10 SDK and Node 22+.

```bash
# API — http://127.0.0.1:47221
export PATH="$HOME/.dotnet:$PATH"
cd src/GisDashboard.Api
dotnet run --urls http://127.0.0.1:47221

# SPA — http://127.0.0.1:47222 (proxies /api to the API)
cd client
npm install
npm run dev
```

Or `chmod +x scripts/dev.sh && ./scripts/dev.sh`.

Demo seed (local / `Seed__Enabled=true` only):

| Username | Email | Full name | Role | Organizations |
| --- | --- | --- | --- | --- |
| `admin` | `admin@bisconsultants.local` | — | Global Administrator | All |
| `admin2` | `admin@democlient.local` | — | Administrator | Demo Client |
| `arivera` | `editor@bisconsultants.local` | Alex Rivera | Editor | Demo Client |
| `jhale` | `viewer@bisconsultants.local` | Jordan Hale | Viewer | Demo Client |
| `admin3` | `admin@otherclient.local` | — | Administrator | Other Client |
| `cnguyen` | `editor.other@bisconsultants.local` | Casey Nguyen | Editor | Other Client |

Password for all demo users: `Demo!Gis2026`

`Other Client` exists so IDOR tests can prove a Demo Client editor or org Administrator cannot open another organization's documents.

## Tests

```bash
dotnet test GisDashboard.slnx
```

The suite covers unauthenticated 401s, Viewer org-scoped 404s (IDOR), staff all-organization document access, Viewer upload to own org, Global Administrator environment status (live DB/storage, 403 for other roles), Internal Notes visibility and history (staff only — Viewers cannot read notes), time-log CRUD/role gates, directory scoping, org-upload resolution (id / name / stale seed id), dashboard KPIs and chart series, bucket/export APIs, comments (Viewers can post; client-visible comment email + @mention bells), status-change and public-upload-received emails (fail closed without SMTP), Phase 3 detail fields, organization/user edit authorization, tokenized public upload (happy path, bad token, cross-org), monthly report generation, history isolation by organization, report recipient authorization, time report cards (own hours vs team, per-org client toggle, IDOR, CSV), priority work (client flag + note + needed-by date, Viewer 403, Priority and Due this week buckets, Dashboard KPI, token-upload notify, IDOR, assigned technicians, primary assigned tech), mass upload (two files → two work items, per-file size rejection that does not block the next file), Cancelled status, document-type order, Reviewed flag, auto-assign to the organization’s primary Assigned tech then Editor, AI fill from PDF (fail closed when Azure OpenAI is unset, Viewer 403 / cross-org 404, image and empty-text rejection, configured fill does not persist), Global Administrator SMTP settings (role gate, write-only password, fail-closed test send, Dashboard Send report uses saved SMTP), forgot password (generic response, no enumeration, fail-closed without SMTP, 30-minute single-use token, rate limit), and presence (heartbeat upsert, 3-minute expiry, Viewer 403 on the list, Editor same-org scoping, Global Administrator sees all, Clocked in and work-item title).

## Roles and permissions

Two axes: **scope** (all organizations vs assigned client orgs) and **org permission** (Administrator / Editor / Viewer). There is no Client role and no “BIS” / “God Rights” role label.

| Capability | Global Administrator | Administrator | Editor | Viewer |
| --- | --- | --- | --- | --- |
| Organization scope (documents) | All orgs | All orgs | All orgs | Assigned org(s) |
| Upload | Yes | Yes | Yes | Yes (own org) |
| Change status / assignment / title | Yes | Yes | Yes | No |
| Internal Notes (staff only) | Edit | Edit | Edit | No |
| Time logs | Manage all | Manage all in org | Own entries | Read |
| Floating time clock | Yes | Yes | Yes | No |
| Comments (client visible) | Post | Post | Post | Post |
| Users | All users | Users in assigned orgs | No | No |
| Create organizations | Yes | No | No | No |
| View monthly reports | All orgs | All orgs | All orgs | Assigned org(s) |
| Generate / email reports | Yes | All orgs | All orgs | No |
| Time report card (own hours) | Yes | Own hours always | Own hours always | Only if that org’s client toggle is on |
| Time report card (team hours) | Always | Only if that org’s client toggle is on | No | Only if that org’s client toggle is on |
| Turn on client time report cards | Yes (per org) | No | No | No |
| Mark work item priority | Yes | Yes | Yes | Badge only |
| Clear / acknowledge priority | Yes | Yes | Yes | No |
| Assign organization technicians | Yes | No | No | No |
| Who’s online (presence) | All signed-in users | Same-org users + Global Admins + self | Same-org users + Global Admins + self | No |

Claim values: `GlobalAdministrator`, `Administrator`, `Editor`, `Viewer`. UI shows **Global Administrator** (never “BIS admin”).

**Comments** are client-visible. Viewers can post. A new comment emails the assignee and Assigned tech(s) when SMTP is configured. `@username` in a comment notifies that person in the bell.

**Internal Notes** are staff-only (Global Administrator, Administrator, Editor). Viewers cannot read or edit them. Edits append a lightweight history (who/when/body). Time logs use hours + minutes. The Assigned To **user id** is never shown as a grid *column* — Editors change assignee with a name dropdown.

**Status-change email** (SMTP) goes to org client contacts when known (org Administrator / Viewer with email), Assigned tech(s), and the work-item assignee. Subject is `[Organization] Title: Old → New`. If SMTP is off, the status still saves and the email is skipped (fail closed).

**Manage Documents** (Editor+): status and assignee can be changed inline. **My queue** presets — Assigned to me, Priority, Due this week, Clear — persist in `localStorage` (`gis.myQueue`).

**Needed by** is a date on Priority work. The grid and detail show a chip; overdue items are marked. Due this week is a My queue / bucket filter. Priority alerts include the needed-by date.

Each organization can mark a **Primary assigned tech**. New uploads prefer that person, then an Editor.

**Who’s online:** while signed in, the browser sends a heartbeat about every 45 seconds and on each route change (current page, work item when on `/documents/{id}`, and whether the floating clock is running). Staff see a people icon in the header plus a compact card on Dashboard and the Global Administrator **Status** page. Global Administrators see everyone; Editors and organization Administrators see people who share an organization (plus themselves and Global Administrators). Client Viewers do not get the list. Offline after about three minutes without a heartbeat. There is no chat or screen share.

The floating clock writes the same Phase 2 time entries. On a work-item detail route it defaults to that document. Everywhere else the tech must pick a work item (search or My Work Items) before start/stop or add. Time is never unallocated — it always belongs to a GIS work item so it shows on the item and on time report cards. Viewers do not see the clock.

### Role migration (existing Phase 1 databases)

On startup, `RoleModelUpgrade` maps the old four-role model:

- Old all-orgs `Administrator` → `GlobalAdministrator`
- Old `Client` → org-scoped `Administrator` (same assigned organization)
- `Editor` and `Viewer` unchanged
- The `Client` role is removed and is no longer assignable

Local/demo seed also remaps `client@democlient.local` → `admin@democlient.local` and `client@otherclient.local` → `admin@otherclient.local`. Production (`Seed__Enabled=false`) keeps existing emails and only remaps role claims.

## Azure resources (already provisioned)

- Resource group: `rg-bis-gis-dashboard` (South Central US)
- SQL: `gishost01.database.windows.net` / `dbgisdashboard` / SQL login `bisadmin` (password is KV `SqlAdminPassword` — do not invent or commit it)
- Web App: `appgisdashboard` (Windows, .NET 10, B1) — https://appgisdashboard-hffvekhnb5bybxb3.southcentralus-01.azurewebsites.net
- Storage: `stbisgisdashboard`, private container `workfiles`
- Key Vault: `kv-bis-gis-dashboard` (already has `SqlConnectionString`, `SqlAdminPassword`, `StorageConnectionString`)
- App Insights: `appi-bis-gis-dashboard`

## Azure App Settings (names only — no secret values)

Set these on `appgisdashboard`. Prefer Key Vault **references** so secret values never sit in App Settings. The published app also loads the vault when `KeyVaultUri` is set (already on the site) and maps existing KV secret names.

### Required / already expected

| App setting | Value / source |
| --- | --- |
| `KeyVaultUri` | `https://kv-bis-gis-dashboard.vault.azure.net/` (already on the app). Alias `KeyVault__VaultUri` also works. |
| `ConnectionStrings__DefaultConnection` | Key Vault reference to `SqlConnectionString`. SQL host `gishost01.database.windows.net`, database `dbgisdashboard`, login **`bisadmin`**. |
| `ConnectionStrings__AzureStorage` | Key Vault reference to `StorageConnectionString` (`stbisgisdashboard`). |
| `AzureStorage__Enabled` | `true` (Production default) |
| `AzureStorage__AccountName` | `stbisgisdashboard` |
| `AzureStorage__ContainerName` | `workfiles` |
| `Jwt__Issuer` | `gis-dashboard` |
| `Jwt__Audience` | `gis-dashboard` |
| `Jwt__Key` | **Required.** 32+ character signing key. Not in KV yet — set as an App Setting or add KV secret `Jwt--Key` (maps to `Jwt:Key`). Do not reuse the local-only key from the repo. |
| `Jwt__ExpiresMinutes` | `480` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Seed__Enabled` | `false` in Production (default). Set `true` only for a controlled first seed, then turn off. |

Global Administrator **Status** (`/status`, `GET /api/admin/status`) runs checks from the running web app — what this process can actually reach — not Azure Resource Manager.

| Check | Mode | What it proves |
| --- | --- | --- |
| Web app / API | Live | This process is up (`/api/health` + uptime) |
| SQL Server / database | Live | `CanConnect` + `SELECT 1` |
| Blob / storage | Live | `workfiles` container properties (Azure) or a writable local folder |
| Key Vault | Live when `KeyVaultUri` is set | Vault host is reachable; secret **names** can be listed. Values are never returned. |
| Application Insights | Configured only | Connection string is present. Not an ingest ping. |
| Email / SMTP | Configured only | `Email__Enabled` + SMTP host. No test message is sent. |
| Azure OpenAI | Configured only | `AzureOpenAI__Endpoint` + `AzureOpenAI__ApiKey` present. Deployment name is shown. Not a live chat ping. |

Overall is **Down** if the live database check fails, **Degraded** if any other live check fails, otherwise **OK**. Short error text is sanitized (passwords, keys, and long tokens stripped). Connection strings are never returned.

### Optional

| App setting | Purpose |
| --- | --- |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | `appi-bis-gis-dashboard` |
| `Cors__Origins__0` | Only if the SPA is hosted on a different origin |
| `Email__Enabled` | Fallback when Admin Settings has no saved SMTP. `true` to send. Default `false` = dry-run for maintenance-report email. |
| `Email__From` | From address fallback (e.g. `noreply@bisconsultants.com`) |
| `Email__FromName` | `GIS Dashboard` |
| `Email__ReplyTo` | Optional reply-to fallback |
| `Email__PublicBaseUrl` | Public origin used in report email links, e.g. `https://appgisdashboard-hffvekhnb5bybxb3.southcentralus-01.azurewebsites.net` |
| `Email__Smtp__Host` | SMTP host fallback (Azure Communication Email SMTP, Exchange, or another provider) |
| `Email__Smtp__Port` | `587` typical |
| `Email__Smtp__User` | SMTP user |
| `Email__Smtp__Password` | Prefer Key Vault secret `Email--Smtp--Password`. A Global Administrator can also save a write-only password under **Admin Settings → Email / SMTP** (encrypted in the app database; Key Vault write is attempted when the vault URI is set). Never returned to the SPA. |
| `Email__Smtp__UseSsl` | `true` |
| `Uploads__MaxFileMegabytes` | Per-file upload limit in MiB. **Default 50** if unset. Typical 25–50. |
| `Uploads__MaxFileBytes` | Same limit in bytes (used when megabytes is unset). Default `52428800`. |
| `Uploads__Concurrency` | How many browser uploads run at once. Default `3`. |
| `AzureOpenAI__Endpoint` | Azure OpenAI resource endpoint, e.g. `https://oai-bis-deed-ai.openai.azure.com/`. Required for **AI fill from PDF**. |
| `AzureOpenAI__ApiKey` | Key Vault **reference only** (never a raw key in the Portal or repo). Alias `AZURE_OPENAI_API_KEY`. |
| `AzureOpenAI__Deployment` | Chat deployment name. Default `gpt-4.1-mini`. |
| `AzureOpenAI__TimeoutSeconds` | Per-attempt chat timeout. Default `45`. |
| `AzureOpenAI__MaxRetries` | Extra chat attempts after a failure. Default `2`. |

A Global Administrator configures SMTP on **Admin Settings → Email / SMTP** (host, port, TLS/SSL, From, optional display name, username, write-only password, optional reply-to). **Save** persists immediately. **Send test email** asks for a To address and confirms before sending. Dashboard **Send report** fail-closes with 503 when SMTP is not configured. When `Email__Enabled` is false or no host is set (and nothing is saved in Settings), `POST /api/reports/{id}/email` still records a **dry-run** log and returns `mode: dry-run`. `emailed` / `lastEmailedAt` stay empty so operators never see a fake send.

### Preferred Key Vault references (Portal)

```
ConnectionStrings__DefaultConnection = @Microsoft.KeyVault(SecretUri=https://kv-bis-gis-dashboard.vault.azure.net/secrets/SqlConnectionString/)
ConnectionStrings__AzureStorage      = @Microsoft.KeyVault(SecretUri=https://kv-bis-gis-dashboard.vault.azure.net/secrets/StorageConnectionString/)
```

If those App Settings are omitted but `KeyVaultUri` is set and the app identity can read secrets, the app also accepts the raw KV names `SqlConnectionString` and `StorageConnectionString`. `SqlAdminPassword` is for operators / connection-string construction — the app does not read it.

Local defaults stay on SQLite (`Data Source=gisdashboard.db`) and `workfiles/` on disk.

## Publish / zipdeploy (Layout A)

```bash
dotnet publish src/GisDashboard.Api/GisDashboard.Api.csproj -c Release -o publish
# Builds the Vite SPA into wwwroot. Skip with -p:SkipSpaBuild=true if needed.
cd publish && zip -r ../artifacts/gisdashboard-phase3.1.zip .
```

Zip root must be the site content (`GisDashboard.Api.dll`, `web.config`, `wwwroot/`), not a nested `publish/` folder.

Portal / Kudu zipdeploy onto Windows App Service `appgisdashboard`. Do not set `WEBSITE_RUN_FROM_PACKAGE=1` unless SQL + Blob settings are in place — that mount is read-only and local SQLite/`workfiles` cannot be written.

## Later v1 phases (not cuts)

These stay in v1 and have extension points in settings / the Sketch? flag / ephemeral viewer highlights. They are **not** implemented in this Phase 3.1 slice:

- Client shell polish
- Live parcel-inventory extract (Total Real Accounts / ownership) — fields exist on the organization and snapshot as placeholders until a source is wired
- Shapefile zip
- Retention / migration
- Auto-save + resume
- **DeedAi/sketch linking** (Sketch? boolean is the hook)
- **Entra SSO**
- **Persisted PDF markup** (highlights are ephemeral for now)

### Auth and password reset

| Method | Path | Auth |
| --- | --- | --- |
| POST | `/api/auth/login` | Anonymous |
| GET | `/api/auth/password-reset` | Anonymous — `{ available }` (SMTP configured or not; not a user check) |
| POST | `/api/auth/forgot-password` | Anonymous — always generic when SMTP is configured; 503 if it is not. Rate-limited (5 / 15 min / IP). |
| POST | `/api/auth/reset-password` | Anonymous — token + new password + confirm |
| — | `/forgot-password` | Public SPA page |
| — | `/reset-password?token=` | Public SPA page |

### Environment status

| Method | Path | Auth |
| --- | --- | --- |
| GET | `/api/admin/status` | Global Administrator only |

### Tokenized upload routes

| Method | Path | Auth |
| --- | --- | --- |
| GET | `/api/admin/organizations/{id}/upload-link` | Global Admin or org Admin (own org) |
| POST | `/api/admin/organizations/{id}/upload-link/regenerate` | same |
| GET | `/api/public/uploads/{token}` | Anonymous — org name + document types only |
| POST | `/api/public/uploads/{token}` | Anonymous — one multipart file per request (the SPA queues many files as separate POSTs) |
| — | `/upload/{token}` | Public SPA page — multi-select / drag-drop. Shows a read-only **Client** field (organization display name from the token). |

### Dashboard export

| Method | Path | Auth |
| --- | --- | --- |
| GET | `/api/dashboard?from=&to=` | Signed-in. `from`/`to` default to the last 30 days. |
| GET | `/api/dashboard/pdf` | Signed-in — PDF of the current filtered view |
| GET | `/api/dashboard/recipients?organizationId=` | Editor / Administrator / Global Administrator |
| POST | `/api/dashboard/email?from=&to=` | same — `{ userIds, extraEmails }`. **503** if `Email__Enabled` / `Email__Smtp__Host` are unset. |

Empty or unknown tokens return 404. The token cannot call admin or work-item APIs.

### AI fill from PDF

| Method | Path | Auth |
| --- | --- | --- |
| POST | `/api/work-items/{id}/ai-fill` | Editor / Administrator / Global Administrator (`CanMutateWorkItems`). Viewer 403; other-org Viewer 404. |

Returns structured fields + per-field confidence (0–1) and `overallConfidence`. Does **not** PATCH the work item. Unconfigured OpenAI returns **503** with the App Setting names. Image or empty-text PDFs return **400**. Text is extracted with PdfPig only — no OCR and no Document Intelligence field-fill.

### Monthly report routes

Viewers of that organization can read reports. Generate and email require Global Administrator, org Administrator, or Editor (`CanMutateWorkItems`). Listing or opening another organization's report returns **404** (same IDOR pattern as work items). Selected email recipients must belong to that organization; extra addresses are free-form.

| Method | Path | Auth |
| --- | --- | --- |
| GET | `/api/reports?organizationId=` | Signed-in, org-scoped (Viewers included) |
| GET | `/api/reports/{id}` | same |
| POST | `/api/reports/generate` | Global / org Admin / Editor — `{ organizationId, cadence: Monthly\|Annual, year, month? }` |
| GET | `/api/reports/recipients?organizationId=` | same (email picker) |
| POST | `/api/reports/{id}/email` | same — `{ userIds, extraEmails }` + PDF attachment |
| GET | `/api/reports/{id}/pdf` | Signed-in, org-scoped (Viewers included) — QuestPDF, not Telerik |
| GET | `/api/reports/{id}/csv` | same — completed maintenance items |
| — | `/reports` | SPA history + generate |
| — | `/reports/{id}` | SPA GIS Maintenance Report |

Generate writes a **new version** for that org + calendar month. Prior versions stay readable. Email is HTML plus a sign-in link and the PDF. No public unauthenticated report URL.

Two cadences share one history table (`Cadence` + period). Monthly matches the Austin sample (parcel status + month totals + completed items). Annual matches the Red River sample (no parcel block; **Total Maintenance Items Completed in {Year}** by Sketches / Plats / Deeds / Corrections; completed-item table that paginates). Title is **GIS Maintenance Report** or **GIS Annual Maintenance Report**. Demo seed fills **Demo Client** parcel stubs (12,840 real accounts / 12,416 with ownership) and completed work for **September 2026**, **August 2026**, earlier 2026 months, and **2025** so monthly and annual reports are not empty. Other Client has a smaller September / August / July 2026 set. Promo banner and Telerik footer are omitted. UI copy says **Client**, never Cad/CAD.

### Time report card routes

Hours come from existing Phase 2 time logs on GIS work items (not a separate timesheet). BIS staff (Editors and the Global Administrator) always reach the card; Global Administrators always see the team. Client Viewers see the card only when `Organization.TimeReportCardsVisible` is on. Org Administrators always see their own hours and see the team only when that toggle is on. The Global Administrator sets the toggle on Organizations → Edit.

| Method | Path | Auth |
| --- | --- | --- |
| GET | `/api/time-report?from=&to=&organizationId=&userId=&bucket=day\|week\|month` | Signed-in; Viewers need the client toggle |
| GET | `/api/time-report/export` | same — CSV of line items (names, not user ids) |
| — | `/time-report` | SPA time report card (print from the browser) |

The floating clock uses the existing work-item time-entry APIs (`POST /api/work-items/{id}/time-entries`) after the tech picks a document (or the open detail route supplies it).

### Priority work

Clients (org Administrator / Editor) mark a GIS work item **Priority** on the work-item page, on signed-in upload, or on the no-login upload link, with an optional short note (“needed by Friday”). Viewers see the badge only. Technicians see a red Priority tag on Manage Documents and work-item cards, a **Priority** bucket and Dashboard KPI, and an in-app header notification (20s poll; no Outlook ingest). Clearing the Priority checkbox acknowledges the item.

A Global Administrator sets **Assigned tech(s)** on Organizations → Edit — the primary GIS contact(s) for that client. Staff org membership on Users and Assigned tech(s) stay in sync: assigning a person to a client on either screen updates the other. Viewers appear under **Members** only. Notifications go to those techs, the work-item Assigned To when set, and every Global Administrator — not a blast to every technician. Saving a work item with Priority on creates or updates the bell row (not only client or token upload).

**Notify rule**

1. Always notify the organization’s assigned tech(s).
2. Also notify the work-item **Assigned To** when that field is set (union — both, not either/or).
3. Always notify every **Global Administrator**. A Global Admin who flagged the item still sees it in the bell.
4. Other people who flag an item (org Admin / Editor) are not notified of their own flag. The token-upload system user is never a recipient.

| Method | Path | Auth |
| --- | --- | --- |
| PATCH | `/api/work-items/{id}` | `{ isPriority, priorityNote }` — Global / org Admin / Editor |
| POST | `/api/work-items` and `/api/public/uploads/{token}` | multipart `isPriority`, `priorityNote` |
| GET | `/api/work-items?bucket=priority` | Signed-in, org-scoped |
| GET | `/api/notifications` | Signed-in — current user’s alerts |
| POST | `/api/notifications/{id}/read` | same |
| POST | `/api/notifications/read-all` | same |
| PUT | `/api/admin/organizations/{id}` | Global Admin may set `assignedTechIds` |

Published zip: `artifacts/gisdashboard-phase3.1.zip`.

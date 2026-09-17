using GisDashboard.Application.Abstractions;
using GisDashboard.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GisDashboard.Infrastructure.Persistence;

public sealed class DemoSeed
{
    public const string DemoPassword = "Demo!Gis2026";

    private static readonly Dictionary<string, string> PreviousEmails = new(StringComparer.OrdinalIgnoreCase)
    {
        ["admin@democlient.local"] = "client@democlient.local",
        ["admin@otherclient.local"] = "client@otherclient.local"
    };

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole<Guid>> _roles;
    private readonly IFileStorage _storage;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DemoSeed> _logger;

    public DemoSeed(
        AppDbContext db,
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole<Guid>> roles,
        IFileStorage storage,
        IConfiguration configuration,
        ILogger<DemoSeed> logger)
    {
        _db = db;
        _users = users;
        _roles = roles;
        _storage = storage;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var enabled = _configuration.GetValue("Seed:Enabled", true);
        if (!enabled)
        {
            return;
        }

        if (_configuration.GetValue("Seed:Recreate", false))
        {
            await _db.Database.EnsureDeletedAsync(cancellationToken);
        }

        await _db.Database.EnsureCreatedAsync(cancellationToken);
        await SchemaUpgrade.ApplyAsync(_db, cancellationToken);

        foreach (var role in Roles.All)
        {
            if (!await _roles.RoleExistsAsync(role))
            {
                var created = await _roles.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.NewGuid() });
                if (!created.Succeeded)
                {
                    throw new InvalidOperationException($"Failed to seed role {role}: {Format(created)}");
                }
            }
        }

        await UpsertOrganizationAsync(
            SeedIds.DemoClient,
            "Demo Client",
            "DEMOCLIENT",
            DateTimeOffset.Parse("2024-01-15T08:00:00-06:00"),
            cancellationToken);
        await UpsertOrganizationAsync(
            SeedIds.OtherClient,
            "Other Client",
            "OTHERCLIENT",
            DateTimeOffset.Parse("2024-02-01T08:00:00-06:00"),
            cancellationToken);

        if (!await _db.DocumentTypes.AnyAsync(cancellationToken))
        {
            _db.DocumentTypes.AddRange(
                new DocumentType { Id = SeedIds.TypeDeed, Name = "Deed", SortOrder = 1 },
                new DocumentType { Id = SeedIds.TypePlat, Name = "Plat", SortOrder = 2 },
                new DocumentType { Id = SeedIds.TypeSurvey, Name = "Survey", SortOrder = 3 },
                new DocumentType { Id = SeedIds.TypeSubdivision, Name = "Subdivision", SortOrder = 4 },
                new DocumentType { Id = SeedIds.TypeOther, Name = "Other", SortOrder = 5 });
        }

        if (!await _db.WorkItemStatuses.AnyAsync(cancellationToken))
        {
            _db.WorkItemStatuses.AddRange(
                new WorkItemStatus { Id = SeedIds.StatusPending, Name = "Pending", Color = "#faad14", SortOrder = 1 },
                new WorkItemStatus { Id = SeedIds.StatusInProgress, Name = "In Progress", Color = "#1890ff", SortOrder = 2 },
                new WorkItemStatus { Id = SeedIds.StatusHeld, Name = "Held", Color = "#fa8c16", SortOrder = 3 },
                new WorkItemStatus { Id = SeedIds.StatusWorked, Name = "Worked", Color = "#52c41a", SortOrder = 4 },
                new WorkItemStatus { Id = SeedIds.StatusQcd, Name = "QC'd", Color = "#722ed1", SortOrder = 5 },
                new WorkItemStatus { Id = SeedIds.StatusCancelled, Name = "Cancelled", Color = "#8c8c8c", SortOrder = 6 });
        }

        await _db.SaveChangesAsync(cancellationToken);

        await EnsureUserAsync(
            SeedIds.Admin,
            "admin@bisconsultants.local",
            "Global Administrator",
            Roles.GlobalAdministrator,
            []);
        await EnsureUserAsync(
            SeedIds.EditorDemo,
            "editor@bisconsultants.local",
            "Alex Rivera",
            Roles.Editor,
            [SeedIds.DemoClient]);
        await EnsureUserAsync(
            SeedIds.ViewerDemo,
            "viewer@bisconsultants.local",
            "Jordan Hale",
            Roles.Viewer,
            [SeedIds.DemoClient]);
        await EnsureUserAsync(
            SeedIds.OrgAdminDemo,
            "admin@democlient.local",
            "Demo Client Administrator",
            Roles.Administrator,
            [SeedIds.DemoClient]);
        await EnsureUserAsync(
            SeedIds.EditorOther,
            "editor.other@bisconsultants.local",
            "Casey Nguyen",
            Roles.Editor,
            [SeedIds.OtherClient]);
        await EnsureUserAsync(
            SeedIds.OrgAdminOther,
            "admin@otherclient.local",
            "Other Client Administrator",
            Roles.Administrator,
            [SeedIds.OtherClient]);

        if (!await _db.OrganizationTechs.AnyAsync(x => x.OrganizationId == SeedIds.DemoClient && x.UserId == SeedIds.EditorDemo, cancellationToken))
        {
            _db.OrganizationTechs.Add(new OrganizationTech
            {
                OrganizationId = SeedIds.DemoClient,
                UserId = SeedIds.EditorDemo,
                IsPrimary = true
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        if (!await _db.WorkItems.AnyAsync(cancellationToken))
        {
            await SeedWorkItemAsync(
                SeedIds.DemoPlat,
                SeedIds.DemoClient,
                SeedIds.TypePlat,
                SeedIds.StatusInProgress,
                SeedIds.EditorDemo,
                "N-14-042 Plat.pdf",
                "Northridge Addition, Block 4",
                "Need bearing check on the west line before QC.",
                SampleFiles.MinimalPdf("Demo Client Plat N-14-042"),
                "application/pdf",
                DateTimeOffset.Parse("2026-09-05T09:14:00-05:00"),
                cancellationToken);

            await SeedWorkItemAsync(
                SeedIds.DemoSurvey,
                SeedIds.DemoClient,
                SeedIds.TypeSurvey,
                SeedIds.StatusPending,
                null,
                "Smith-Tract-Survey.pdf",
                "Smith 12.4 ac survey",
                "Waiting on client account match.",
                SampleFiles.MinimalPdf("Demo Client Survey Smith Tract"),
                "application/pdf",
                DateTimeOffset.Parse("2026-09-02T14:32:00-05:00"),
                cancellationToken);

            await SeedWorkItemAsync(
                SeedIds.DemoDeed,
                SeedIds.DemoClient,
                SeedIds.TypeDeed,
                SeedIds.StatusQcd,
                SeedIds.EditorDemo,
                "Warranty-Deed-scan.png",
                "Warranty deed scan",
                "QC complete. Ready for shapefile later phase.",
                SampleFiles.TinyPng(),
                "image/png",
                DateTimeOffset.Parse("2026-07-21T11:05:00-05:00"),
                cancellationToken);

            await SeedWorkItemAsync(
                SeedIds.DemoHeld,
                SeedIds.DemoClient,
                SeedIds.TypeOther,
                SeedIds.StatusHeld,
                SeedIds.EditorDemo,
                "Boundary-question.pdf",
                "Boundary question packet",
                "Held for client clarification on lot split.",
                SampleFiles.MinimalPdf("Demo Client Held Boundary Question"),
                "application/pdf",
                DateTimeOffset.Parse("2026-06-30T16:40:00-05:00"),
                cancellationToken);

            await SeedWorkItemAsync(
                SeedIds.DemoOakGrove,
                SeedIds.DemoClient,
                SeedIds.TypePlat,
                SeedIds.StatusWorked,
                SeedIds.EditorDemo,
                "Oak-Grove-Replat.pdf",
                "Oak Grove replat",
                "Worked. Pending QC assignment.",
                SampleFiles.MinimalPdf("Demo Client Oak Grove Replat"),
                "application/pdf",
                DateTimeOffset.Parse("2026-08-28T10:18:00-05:00"),
                cancellationToken);

            await SeedWorkItemAsync(
                SeedIds.OtherPlat,
                SeedIds.OtherClient,
                SeedIds.TypePlat,
                SeedIds.StatusPending,
                SeedIds.EditorOther,
                "Other-Client-Confidential-Plat.pdf",
                "Other Client confidential plat",
                "Internal only — used for IDOR isolation tests.",
                SampleFiles.MinimalPdf("Other Client Confidential Plat"),
                "application/pdf",
                DateTimeOffset.Parse("2026-09-04T08:22:00-05:00"),
                cancellationToken);
            await SeedPhase2Async(cancellationToken);
            await SeedPhase3Async(cancellationToken);
        }
        else
        {
            await RefreshSeedWorkItemCopyAsync(
                SeedIds.DemoPlat,
                "N-14-042 Plat.pdf",
                "Northridge Addition, Block 4",
                "Need bearing check on the west line before QC.",
                SampleFiles.MinimalPdf("Demo Client Plat N-14-042"),
                cancellationToken);
            await RefreshSeedWorkItemCopyAsync(
                SeedIds.DemoSurvey,
                "Smith-Tract-Survey.pdf",
                "Smith 12.4 ac survey",
                "Waiting on client account match.",
                SampleFiles.MinimalPdf("Demo Client Survey Smith Tract"),
                cancellationToken);
            await RefreshSeedWorkItemCopyAsync(
                SeedIds.DemoHeld,
                "Boundary-question.pdf",
                "Boundary question packet",
                "Held for client clarification on lot split.",
                SampleFiles.MinimalPdf("Demo Client Held Boundary Question"),
                cancellationToken);
            await RefreshSeedWorkItemCopyAsync(
                SeedIds.DemoOakGrove,
                "Oak-Grove-Replat.pdf",
                "Oak Grove replat",
                "Worked. Pending QC assignment.",
                SampleFiles.MinimalPdf("Demo Client Oak Grove Replat"),
                cancellationToken);
            await RefreshSeedWorkItemCopyAsync(
                SeedIds.OtherPlat,
                "Other-Client-Confidential-Plat.pdf",
                "Other Client confidential plat",
                "Internal only — used for IDOR isolation tests.",
                SampleFiles.MinimalPdf("Other Client Confidential Plat"),
                cancellationToken);
            await SeedPhase2Async(cancellationToken);
            await SeedPhase3Async(cancellationToken);
        }

        await ApplyOrgDemoInventoryAsync(cancellationToken);
        await SeedReportCatalogAsync(cancellationToken);
        await SeedDemoConnectionAsync(cancellationToken);

        _logger.LogInformation("Demo seed ready. Local passwords use Seed:DemoPassword / documented demo password.");
    }

    private async Task UpsertOrganizationAsync(
        Guid id,
        string name,
        string code,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        var existing = await _db.Organizations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null)
        {
            _db.Organizations.Add(new Organization
            {
                Id = id,
                Name = name,
                Code = code,
                IsActive = true,
                CreatedAt = createdAt,
                UploadToken = Security.UploadTokens.Create(),
                UploadTokenCreatedAt = DateTimeOffset.UtcNow
            });
            return;
        }

        existing.Name = name;
        existing.Code = code;
        if (string.IsNullOrWhiteSpace(existing.UploadToken))
        {
            existing.UploadToken = Security.UploadTokens.Create();
            existing.UploadTokenCreatedAt = DateTimeOffset.UtcNow;
        }
    }

    private async Task EnsureUserAsync(
        Guid id,
        string email,
        string displayName,
        string role,
        IReadOnlyCollection<Guid> organizationIds)
    {
        var existing = await _users.FindByIdAsync(id.ToString());
        if (existing is null)
        {
            existing = await _users.FindByEmailAsync(email);
        }

        if (existing is null && PreviousEmails.TryGetValue(email, out var previousEmail))
        {
            existing = await _users.FindByEmailAsync(previousEmail);
        }

        if (existing is null)
        {
            var user = new ApplicationUser
            {
                Id = id,
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName,
                FullName = UserIdentity.LooksLikePersonName(displayName) ? UserIdentity.ToTitleCase(displayName) : null,
                IsActive = true,
                CreatedAt = DateTimeOffset.Parse("2026-01-10T08:00:00-06:00")
            };

            var created = await _users.CreateAsync(user, DemoPassword);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException($"Failed to seed user {email}: {Format(created)}");
            }

            existing = user;
        }
        else if (!string.Equals(existing.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var emailResult = await _users.SetEmailAsync(existing, email);
            if (!emailResult.Succeeded)
            {
                throw new InvalidOperationException($"Failed to update email for {email}: {Format(emailResult)}");
            }
        }

        var roles = await _users.GetRolesAsync(existing);
        if (!roles.Contains(role))
        {
            foreach (var extra in roles)
            {
                await _users.RemoveFromRoleAsync(existing, extra);
            }

            var added = await _users.AddToRoleAsync(existing, role);
            if (!added.Succeeded)
            {
                throw new InvalidOperationException($"Failed to assign role {role} to {email}: {Format(added)}");
            }
        }

        foreach (var orgId in organizationIds)
        {
            var linked = await _db.UserOrganizations.AnyAsync(x => x.UserId == existing.Id && x.OrganizationId == orgId);
            if (!linked)
            {
                _db.UserOrganizations.Add(new UserOrganization
                {
                    UserId = existing.Id,
                    OrganizationId = orgId
                });
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task SeedWorkItemAsync(
        Guid id,
        Guid organizationId,
        Guid documentTypeId,
        Guid statusId,
        Guid? assignedToUserId,
        string fileName,
        string title,
        string internalNotes,
        byte[] bytes,
        string contentType,
        DateTimeOffset uploadedAt,
        CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(bytes);
        var blobPath = await _storage.SaveAsync(organizationId, id, fileName, stream, contentType, cancellationToken);

        var item = new WorkItem
        {
            Id = id,
            OrganizationId = organizationId,
            FileName = fileName,
            Title = title,
            DocumentTypeId = documentTypeId,
            StatusId = statusId,
            AssignedToUserId = assignedToUserId,
            InternalNotes = internalNotes,
            BlobPath = blobPath,
            ContentType = contentType,
            FileSizeBytes = bytes.Length,
            UploadedByUserId = organizationId == SeedIds.DemoClient ? SeedIds.OrgAdminDemo : SeedIds.OrgAdminOther,
            UpdatedByUserId = assignedToUserId ?? (organizationId == SeedIds.DemoClient ? SeedIds.OrgAdminDemo : SeedIds.OrgAdminOther)
        };
        item.TouchDates(uploadedAt, uploadedAt.AddHours(6));
        _db.WorkItems.Add(item);
        _db.InternalNoteRevisions.Add(CreateNoteRevision(id, internalNotes, assignedToUserId ?? item.UpdatedByUserId, uploadedAt.AddHours(1)));

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedPhase2Async(CancellationToken cancellationToken)
    {
        if (!await _db.TimeEntries.AnyAsync(x => x.Id == SeedIds.TimeEditorDemoPlat, cancellationToken))
        {
            var editorLogged = DateTimeOffset.Parse("2026-09-08T10:30:00-05:00");
            var editorEntry = new TimeEntry
            {
                Id = SeedIds.TimeEditorDemoPlat,
                WorkItemId = SeedIds.DemoPlat,
                LoggedByUserId = SeedIds.EditorDemo,
                Minutes = 90,
                Note = "West line bearing check"
            };
            editorEntry.TouchWorkedOn(DateTimeOffset.Parse("2026-09-08T00:00:00-05:00"));
            editorEntry.TouchCreated(editorLogged);
            _db.TimeEntries.Add(editorEntry);
        }

        if (!await _db.TimeEntries.AnyAsync(x => x.Id == SeedIds.TimeAdminDemoPlat, cancellationToken))
        {
            var adminLogged = DateTimeOffset.Parse("2026-09-09T15:05:00-05:00");
            var adminEntry = new TimeEntry
            {
                Id = SeedIds.TimeAdminDemoPlat,
                WorkItemId = SeedIds.DemoPlat,
                LoggedByUserId = SeedIds.Admin,
                Minutes = 45,
                Note = "QC pass on bearings"
            };
            adminEntry.TouchWorkedOn(DateTimeOffset.Parse("2026-09-09T00:00:00-05:00"));
            adminEntry.TouchCreated(adminLogged);
            _db.TimeEntries.Add(adminEntry);
        }

        var itemsWithNotes = await _db.WorkItems
            .Where(x => x.InternalNotes != null && x.InternalNotes != "")
            .Select(x => new { x.Id, x.InternalNotes, x.UpdatedByUserId, x.UpdatedAt })
            .ToListAsync(cancellationToken);

        foreach (var item in itemsWithNotes)
        {
            var hasRevision = await _db.InternalNoteRevisions.AnyAsync(x => x.WorkItemId == item.Id, cancellationToken);
            if (hasRevision)
            {
                continue;
            }

            _db.InternalNoteRevisions.Add(CreateNoteRevision(
                item.Id,
                item.InternalNotes!,
                item.UpdatedByUserId,
                item.UpdatedAt));
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedPhase3Async(CancellationToken cancellationToken)
    {
        var plat = await _db.WorkItems.FirstOrDefaultAsync(x => x.Id == SeedIds.DemoPlat, cancellationToken);
        if (plat is not null)
        {
            plat.IsSketch = true;
            plat.AnnexationCount = 1;
            plat.CorrectionCount = 2;
            plat.DeedCount = 0;
            plat.PlatCount = 1;
            plat.PropertyIds = "R12345\nR12346";
            plat.SetFirstDeadline(DateTimeOffset.Parse("2026-09-18T17:00:00-05:00"));
            plat.SetFinalDeadline(DateTimeOffset.Parse("2026-10-02T17:00:00-05:00"));
        }

        var held = await _db.WorkItems.FirstOrDefaultAsync(x => x.Id == SeedIds.DemoHeld, cancellationToken);
        if (held is not null)
        {
            held.IsSplit = true;
            held.SetFirstDeadline(DateTimeOffset.Parse("2026-09-12T17:00:00-05:00"));
        }

        var worked = await _db.WorkItems.FirstOrDefaultAsync(x => x.Id == SeedIds.DemoOakGrove, cancellationToken);
        if (worked is not null)
        {
            worked.SetWorkedOn(DateTimeOffset.Parse("2026-08-29T16:00:00-05:00"));
            worked.PlatCount = 1;
            worked.CorrectionCount = 2;
            worked.PropertyIds = "R88901\nR88902";
        }

        var deed = await _db.WorkItems.FirstOrDefaultAsync(x => x.Id == SeedIds.DemoDeed, cancellationToken);
        if (deed is not null)
        {
            deed.SetWorkedOn(DateTimeOffset.Parse("2026-07-22T09:30:00-05:00"));
            deed.DeedCount = 1;
        }

        if (!await _db.WorkItemComments.AnyAsync(x => x.Id == SeedIds.CommentDemoPlat, cancellationToken))
        {
            var created = DateTimeOffset.Parse("2026-09-08T11:05:00-05:00");
            _db.WorkItemComments.Add(new WorkItemComment
            {
                Id = SeedIds.CommentDemoPlat,
                WorkItemId = SeedIds.DemoPlat,
                AuthorUserId = SeedIds.EditorDemo,
                Body = "Client asked for a second look at the west line before we mark this complete.",
                CreatedAt = created,
                CreatedAtSort = created.ToUnixTimeMilliseconds()
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedDemoConnectionAsync(CancellationToken cancellationToken)
    {
        if (!await _db.FileServers.AnyAsync(x => x.Id == SeedIds.LocalFileServer, cancellationToken))
        {
            _db.FileServers.Add(new FileServer
            {
                Id = SeedIds.LocalFileServer,
                Name = "Local files",
                RootPath = "workfiles",
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        if (!await _db.FileConnections.AnyAsync(x => x.Id == SeedIds.DemoFileConnection, cancellationToken))
        {
            _db.FileConnections.Add(new FileConnection
            {
                Id = SeedIds.DemoFileConnection,
                OrganizationId = SeedIds.DemoClient,
                FileServerId = SeedIds.LocalFileServer,
                SourcePath = "workfiles/orgs/democlient/shapefiles",
                Enabled = true,
                Status = "Idle",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        if (!await _db.LanConnections.AnyAsync(x => x.Id == SeedIds.DemoLanConnection, cancellationToken))
        {
            _db.LanConnections.Add(new LanConnection
            {
                Id = SeedIds.DemoLanConnection,
                OrganizationId = SeedIds.DemoClient,
                BisFolder = "workfiles/orgs/democlient/shapefiles",
                RemoteFolder = @"C:\GIS\Outgoing",
                Direction = "Bidirectional",
                ScheduleMinutes = 15,
                EnrollTokenHash = "SEED",
                EnrollTokenMasked = "••••••••••••",
                Status = "Idle",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        if (!await _db.SyncControl.AnyAsync(x => x.Id == SyncControlState.SingletonId, cancellationToken))
        {
            _db.SyncControl.Add(new SyncControlState
            {
                Id = SyncControlState.SingletonId,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyOrgDemoInventoryAsync(CancellationToken cancellationToken)
    {
        var demo = await _db.Organizations.FirstAsync(x => x.Id == SeedIds.DemoClient, cancellationToken);
        demo.ParcelTotalRealAccounts = 12840;
        demo.ParcelWithOwnership = 12416;

        var other = await _db.Organizations.FirstAsync(x => x.Id == SeedIds.OtherClient, cancellationToken);
        other.ParcelTotalRealAccounts = 4210;
        other.ParcelWithOwnership = 3892;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedReportCatalogAsync(CancellationToken cancellationToken)
    {
        var catalog = new (Guid Id, Guid OrgId, Guid TypeId, Guid StatusId, Guid Assignee, string FileName, string Title, string Notes, string Uploaded, string Worked, int Annex, int Corr, int Plat, int Deed, bool Sketch, string PropertyIds)[]
        {
            (SeedIds.DemoSepCedarRidge, SeedIds.DemoClient, SeedIds.TypePlat, SeedIds.StatusWorked, SeedIds.EditorDemo,
                "Cedar-Ridge-Plat.pdf", "Cedar Ridge plat", "Completed for September maintenance.",
                "2026-09-03T09:10:00-05:00", "2026-09-04T15:40:00-05:00", 1, 0, 1, 0, true, "R20110\nR20111"),
            (SeedIds.DemoSepMapleCorrection, SeedIds.DemoClient, SeedIds.TypeOther, SeedIds.StatusQcd, SeedIds.EditorDemo,
                "Maple-Street-Correction.pdf", "Maple Street correction", "QC'd correction packet.",
                "2026-09-04T11:20:00-05:00", "2026-09-05T16:05:00-05:00", 0, 3, 0, 0, false, "R20120"),
            (SeedIds.DemoSepRiversideDeed, SeedIds.DemoClient, SeedIds.TypeDeed, SeedIds.StatusWorked, SeedIds.EditorDemo,
                "Riverside-Warranty-Deed.pdf", "Riverside warranty deed", "Deed sketched and worked.",
                "2026-09-05T08:45:00-05:00", "2026-09-06T13:20:00-05:00", 0, 0, 0, 2, false, "R20130\nR20131"),
            (SeedIds.DemoSepAnnexExhibit, SeedIds.DemoClient, SeedIds.TypeOther, SeedIds.StatusQcd, SeedIds.EditorDemo,
                "North-Annexation-Exhibit.pdf", "North annexation exhibit", "Annexation exhibit QC complete.",
                "2026-09-07T10:00:00-05:00", "2026-09-08T14:15:00-05:00", 4, 0, 0, 0, true, "R20140"),
            (SeedIds.DemoSepFinalPlat, SeedIds.DemoClient, SeedIds.TypePlat, SeedIds.StatusWorked, SeedIds.EditorDemo,
                "Willow-Creek-Unit-2-Plat.pdf", "Willow Creek Unit 2 plat", "Final plat worked this month.",
                "2026-09-08T09:30:00-05:00", "2026-09-09T11:50:00-05:00", 0, 1, 2, 0, false, "R20150\nR20151"),
            (SeedIds.DemoAugLakeview, SeedIds.DemoClient, SeedIds.TypePlat, SeedIds.StatusWorked, SeedIds.EditorDemo,
                "Lakeview-Estates-Plat.pdf", "Lakeview Estates plat", "August plat.",
                "2026-08-06T09:00:00-05:00", "2026-08-08T16:10:00-05:00", 0, 0, 1, 0, true, "R18810"),
            (SeedIds.DemoAugCorrectionPkt, SeedIds.DemoClient, SeedIds.TypeOther, SeedIds.StatusQcd, SeedIds.EditorDemo,
                "Highland-Correction-Packet.pdf", "Highland correction packet", "August corrections.",
                "2026-08-12T13:15:00-05:00", "2026-08-15T10:40:00-05:00", 0, 4, 0, 0, false, "R18820\nR18821"),
            (SeedIds.DemoAugQuitclaim, SeedIds.DemoClient, SeedIds.TypeDeed, SeedIds.StatusWorked, SeedIds.EditorDemo,
                "Quitclaim-Deed-Lot-14.pdf", "Quitclaim deed Lot 14", "August deed.",
                "2026-08-18T08:20:00-05:00", "2026-08-21T15:05:00-05:00", 0, 0, 0, 1, false, "R18830"),
            (SeedIds.DemoMarAnnex, SeedIds.DemoClient, SeedIds.TypeOther, SeedIds.StatusQcd, SeedIds.EditorDemo,
                "Spring-Annexation-Map.pdf", "Spring annexation map", "March annexation.",
                "2026-03-12T10:00:00-05:00", "2026-03-18T14:30:00-05:00", 2, 0, 0, 0, false, "R16010"),
            (SeedIds.DemoAprSketch, SeedIds.DemoClient, SeedIds.TypeSurvey, SeedIds.StatusWorked, SeedIds.EditorDemo,
                "Cottonwood-Sketch.pdf", "Cottonwood sketch", "April sketch.",
                "2026-04-16T09:40:00-05:00", "2026-04-22T11:15:00-05:00", 0, 0, 1, 0, true, "R16120"),
            (SeedIds.DemoMayDeed, SeedIds.DemoClient, SeedIds.TypeDeed, SeedIds.StatusQcd, SeedIds.EditorDemo,
                "May-Deed-Package.pdf", "May deed package", "May deeds.",
                "2026-05-08T08:50:00-05:00", "2026-05-14T16:00:00-05:00", 0, 1, 0, 3, false, "R16230\nR16231"),
            (SeedIds.DemoY25Plat, SeedIds.DemoClient, SeedIds.TypePlat, SeedIds.StatusWorked, SeedIds.EditorDemo,
                "2025-Year-End-Plat.pdf", "2025 year-end plat", "Prior-year plat for annual demo.",
                "2025-11-04T09:00:00-06:00", "2025-11-12T15:20:00-06:00", 0, 1, 2, 0, true, "R15001"),
            (SeedIds.DemoY25Deed, SeedIds.DemoClient, SeedIds.TypeDeed, SeedIds.StatusQcd, SeedIds.EditorDemo,
                "2025-December-Deed.pdf", "2025 December deed", "Prior-year deed for annual demo.",
                "2025-11-28T10:10:00-06:00", "2025-12-03T13:45:00-06:00", 0, 0, 0, 1, false, "R15020"),
            (SeedIds.OtherSepPlat, SeedIds.OtherClient, SeedIds.TypePlat, SeedIds.StatusWorked, SeedIds.EditorOther,
                "Other-Client-September-Plat.pdf", "Other Client September plat", "Other Client September completed plat.",
                "2026-09-01T09:00:00-05:00", "2026-09-03T14:00:00-05:00", 0, 0, 1, 0, false, "R33010"),
            (SeedIds.OtherAugDeed, SeedIds.OtherClient, SeedIds.TypeDeed, SeedIds.StatusQcd, SeedIds.EditorOther,
                "Other-Client-August-Deed.pdf", "Other Client August deed", "Other Client August completed deed.",
                "2026-08-14T11:00:00-05:00", "2026-08-19T10:30:00-05:00", 0, 1, 0, 1, false, "R33020"),
            (SeedIds.OtherJulSketch, SeedIds.OtherClient, SeedIds.TypeSurvey, SeedIds.StatusWorked, SeedIds.EditorOther,
                "Other-Client-July-Sketch.pdf", "Other Client July sketch", "Other Client July completed sketch.",
                "2026-07-07T08:30:00-05:00", "2026-07-11T16:45:00-05:00", 1, 0, 0, 0, true, "R33030")
        };

        foreach (var row in catalog)
        {
            await EnsureReportWorkItemAsync(
                row.Id, row.OrgId, row.TypeId, row.StatusId, row.Assignee,
                row.FileName, row.Title, row.Notes,
                DateTimeOffset.Parse(row.Uploaded), DateTimeOffset.Parse(row.Worked),
                row.Annex, row.Corr, row.Plat, row.Deed, row.Sketch, row.PropertyIds,
                cancellationToken);
        }
    }

    private async Task EnsureReportWorkItemAsync(
        Guid id,
        Guid organizationId,
        Guid documentTypeId,
        Guid statusId,
        Guid assignedToUserId,
        string fileName,
        string title,
        string internalNotes,
        DateTimeOffset uploadedAt,
        DateTimeOffset workedOn,
        int annexationCount,
        int correctionCount,
        int platCount,
        int deedCount,
        bool isSketch,
        string propertyIds,
        CancellationToken cancellationToken)
    {
        var item = await _db.WorkItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            var orgLabel = organizationId == SeedIds.DemoClient ? "Demo Client" : "Other Client";
            var bytes = SampleFiles.MinimalPdf($"{orgLabel} {title}");
            await using var stream = new MemoryStream(bytes);
            var blobPath = await _storage.SaveAsync(organizationId, id, fileName, stream, "application/pdf", cancellationToken);
            item = new WorkItem
            {
                Id = id,
                OrganizationId = organizationId,
                FileName = fileName,
                Title = title,
                DocumentTypeId = documentTypeId,
                StatusId = statusId,
                AssignedToUserId = assignedToUserId,
                InternalNotes = internalNotes,
                BlobPath = blobPath,
                ContentType = "application/pdf",
                FileSizeBytes = bytes.Length,
                UploadedByUserId = organizationId == SeedIds.DemoClient ? SeedIds.OrgAdminDemo : SeedIds.OrgAdminOther,
                UpdatedByUserId = assignedToUserId,
                IsSketch = isSketch,
                AnnexationCount = annexationCount,
                CorrectionCount = correctionCount,
                PlatCount = platCount,
                DeedCount = deedCount,
                PropertyIds = propertyIds
            };
            item.TouchDates(uploadedAt, workedOn);
            item.SetWorkedOn(workedOn);
            _db.WorkItems.Add(item);
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        item.FileName = fileName;
        item.Title = title;
        item.DocumentTypeId = documentTypeId;
        item.StatusId = statusId;
        item.AssignedToUserId = assignedToUserId;
        item.InternalNotes = internalNotes;
        item.IsSketch = isSketch;
        item.AnnexationCount = annexationCount;
        item.CorrectionCount = correctionCount;
        item.PlatCount = platCount;
        item.DeedCount = deedCount;
        item.PropertyIds = propertyIds;
        item.TouchDates(uploadedAt, workedOn);
        item.SetWorkedOn(workedOn);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static InternalNoteRevision CreateNoteRevision(Guid workItemId, string body, Guid editedByUserId, DateTimeOffset editedAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItemId,
            Body = body,
            EditedByUserId = editedByUserId,
            EditedAt = editedAt,
            EditedAtSort = editedAt.ToUnixTimeMilliseconds()
        };

    private async Task RefreshSeedWorkItemCopyAsync(
        Guid id,
        string fileName,
        string title,
        string internalNotes,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        var item = await _db.WorkItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return;
        }

        await using var stream = new MemoryStream(bytes);
        var blobPath = await _storage.SaveAsync(item.OrganizationId, item.Id, fileName, stream, item.ContentType ?? "application/pdf", cancellationToken);
        item.FileName = fileName;
        item.Title = title;
        item.InternalNotes = internalNotes;
        item.BlobPath = blobPath;
        item.FileSizeBytes = bytes.Length;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string Format(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(e => $"{e.Code}:{e.Description}"));
}

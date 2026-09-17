using GisDashboard.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

public sealed class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<UserOrganization> UserOrganizations => Set<UserOrganization>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<WorkItemStatus> WorkItemStatuses => Set<WorkItemStatus>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<InternalNoteRevision> InternalNoteRevisions => Set<InternalNoteRevision>();
    public DbSet<WorkItemComment> WorkItemComments => Set<WorkItemComment>();
    public DbSet<MonthlyReport> MonthlyReports => Set<MonthlyReport>();
    public DbSet<ReportEmailLog> ReportEmailLogs => Set<ReportEmailLog>();
    public DbSet<OrganizationTech> OrganizationTechs => Set<OrganizationTech>();
    public DbSet<InAppNotification> Notifications => Set<InAppNotification>();
    public DbSet<EmailSettings> EmailSettings => Set<EmailSettings>();
    public DbSet<CompanyContact> CompanyContacts => Set<CompanyContact>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<UserPresence> UserPresences => Set<UserPresence>();
    public DbSet<FileServer> FileServers => Set<FileServer>();
    public DbSet<FileConnection> FileConnections => Set<FileConnection>();
    public DbSet<LanConnection> LanConnections => Set<LanConnection>();
    public DbSet<SyncControlState> SyncControl => Set<SyncControlState>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Organization>(entity =>
        {
            entity.ToTable("Organizations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.UploadToken).HasMaxLength(80);
            entity.HasIndex(x => x.UploadToken).IsUnique();
        });

        builder.Entity<UserOrganization>(entity =>
        {
            entity.ToTable("UserOrganizations");
            entity.HasKey(x => new { x.UserId, x.OrganizationId });
            entity.HasOne(x => x.User)
                .WithMany(x => x.Organizations)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Organization)
                .WithMany(x => x.Members)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DocumentType>(entity =>
        {
            entity.ToTable("DocumentTypes");
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<WorkItemStatus>(entity =>
        {
            entity.ToTable("WorkItemStatuses");
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Color).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<WorkItem>(entity =>
        {
            entity.ToTable("WorkItems");
            entity.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(260).IsRequired();
            entity.Property(x => x.InternalNotes).HasMaxLength(4000);
            entity.Property(x => x.PropertyIds).HasMaxLength(4000);
            entity.Property(x => x.BlobPath).HasMaxLength(1024);
            entity.Property(x => x.ContentType).HasMaxLength(200);
            entity.Property(x => x.PriorityNote).HasMaxLength(500);
            entity.HasIndex(x => new { x.OrganizationId, x.IsPriority });
            entity.HasIndex(x => new { x.OrganizationId, x.UploadedAt });
            entity.HasIndex(x => x.StatusId);
            entity.HasIndex(x => x.DocumentTypeId);

            entity.HasOne(x => x.Organization)
                .WithMany(x => x.WorkItems)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.DocumentType)
                .WithMany()
                .HasForeignKey(x => x.DocumentTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Status)
                .WithMany()
                .HasForeignKey(x => x.StatusId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AssignedToUser)
                .WithMany()
                .HasForeignKey(x => x.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.UploadedByUser)
                .WithMany()
                .HasForeignKey(x => x.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.TimeEntries)
                .WithOne(x => x.WorkItem)
                .HasForeignKey(x => x.WorkItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.NoteRevisions)
                .WithOne(x => x.WorkItem)
                .HasForeignKey(x => x.WorkItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Comments)
                .WithOne(x => x.WorkItem)
                .HasForeignKey(x => x.WorkItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WorkItemComment>(entity =>
        {
            entity.ToTable("WorkItemComments");
            entity.Property(x => x.Body).HasMaxLength(2000).IsRequired();
            entity.HasIndex(x => new { x.WorkItemId, x.CreatedAtSort });
            entity.HasOne(x => x.AuthorUser)
                .WithMany()
                .HasForeignKey(x => x.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TimeEntry>(entity =>
        {
            entity.ToTable("TimeEntries");
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.HasIndex(x => new { x.WorkItemId, x.WorkedOnSort });
            entity.HasOne(x => x.LoggedByUser)
                .WithMany()
                .HasForeignKey(x => x.LoggedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InternalNoteRevision>(entity =>
        {
            entity.ToTable("InternalNoteRevisions");
            entity.Property(x => x.Body).HasMaxLength(4000);
            entity.HasIndex(x => new { x.WorkItemId, x.EditedAtSort });
            entity.HasOne(x => x.EditedByUser)
                .WithMany()
                .HasForeignKey(x => x.EditedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.FullName).HasMaxLength(200);
            entity.Property(x => x.WorkPhone).HasMaxLength(40);
            entity.Property(x => x.AvatarBlobPath).HasMaxLength(1024);
            entity.Property(x => x.AvatarContentType).HasMaxLength(100);
        });

        builder.Entity<MonthlyReport>(entity =>
        {
            entity.ToTable("MonthlyReports");
            entity.Property(x => x.MonthLabel).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Cadence).HasMaxLength(20).IsRequired();
            entity.Property(x => x.SnapshotJson).IsRequired();
            entity.Property(x => x.LastEmailedTo).HasMaxLength(2000);
            entity.HasIndex(x => new { x.OrganizationId, x.Cadence, x.Year, x.Month, x.Version });
            entity.HasOne(x => x.Organization)
                .WithMany()
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.GeneratedByUser)
                .WithMany()
                .HasForeignKey(x => x.GeneratedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Emails)
                .WithOne(x => x.Report)
                .HasForeignKey(x => x.ReportId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OrganizationTech>(entity =>
        {
            entity.ToTable("OrganizationTechs");
            entity.HasKey(x => new { x.OrganizationId, x.UserId });
            entity.HasOne(x => x.Organization)
                .WithMany(x => x.AssignedTechs)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<InAppNotification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.Property(x => x.Kind).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.CreatedAtSort });
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ReportEmailLog>(entity =>
        {
            entity.ToTable("ReportEmailLogs");
            entity.Property(x => x.Recipients).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Subject).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Mode).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Error).HasMaxLength(1000);
            entity.HasIndex(x => new { x.ReportId, x.SentAt });
        });

        builder.Entity<EmailSettings>(entity =>
        {
            entity.ToTable("EmailSettings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Host).HasMaxLength(200);
            entity.Property(x => x.FromAddress).HasMaxLength(200);
            entity.Property(x => x.FromName).HasMaxLength(200);
            entity.Property(x => x.UserName).HasMaxLength(200);
            entity.Property(x => x.ReplyTo).HasMaxLength(200);
            entity.Property(x => x.PasswordProtected).HasMaxLength(4000);
        });

        builder.Entity<CompanyContact>(entity =>
        {
            entity.ToTable("CompanyContact");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.Address).HasMaxLength(300);
            entity.Property(x => x.Website).HasMaxLength(200);
        });

        builder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("PasswordResetTokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(88).IsRequired();
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.CreatedAt });
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserPresence>(entity =>
        {
            entity.ToTable("UserPresence");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.Route).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.LastSeenSort);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FileServer>(entity =>
        {
            entity.ToTable("FileServers");
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.RootPath).HasMaxLength(1024).IsRequired();
        });

        builder.Entity<FileConnection>(entity =>
        {
            entity.ToTable("FileConnections");
            entity.Property(x => x.SourcePath).HasMaxLength(1024).IsRequired();
            entity.Property(x => x.FtpFolder).HasMaxLength(260);
            entity.Property(x => x.FtpUrl).HasMaxLength(500);
            entity.Property(x => x.FtpUserName).HasMaxLength(200);
            entity.Property(x => x.Status).HasMaxLength(40);
            entity.Property(x => x.LastError).HasMaxLength(2000);
            entity.Property(x => x.LastZipName).HasMaxLength(260);
            entity.HasIndex(x => x.OrganizationId);
            entity.HasOne(x => x.Organization)
                .WithMany()
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.FileServer)
                .WithMany()
                .HasForeignKey(x => x.FileServerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<LanConnection>(entity =>
        {
            entity.ToTable("LanConnections");
            entity.Property(x => x.RemoteFolder).HasMaxLength(1024).IsRequired();
            entity.Property(x => x.BisFolder).HasMaxLength(1024).IsRequired();
            entity.Property(x => x.Direction).HasMaxLength(20).IsRequired();
            entity.Property(x => x.EnrollTokenHash).HasMaxLength(88).IsRequired();
            entity.Property(x => x.EnrollTokenMasked).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.LastError).HasMaxLength(2000);
            entity.Property(x => x.LastErrorCode).HasMaxLength(80);
            entity.Property(x => x.WindowsUserName).HasMaxLength(200);
            entity.HasIndex(x => x.OrganizationId);
            entity.HasOne(x => x.Organization)
                .WithMany()
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SyncControlState>(entity =>
        {
            entity.ToTable("SyncControl");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Message).HasMaxLength(500);
        });
    }
}

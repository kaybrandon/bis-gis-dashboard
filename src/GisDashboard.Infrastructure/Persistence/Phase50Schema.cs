namespace GisDashboard.Infrastructure.Persistence;

/// <summary>
/// QC03 — bring existing Editor/Administrator accounts into all-organization membership.
/// Safe to run on every startup. Does not grant Viewer/Uploader all-org access.
/// </summary>
public static class Phase50Schema
{
    public static Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default) =>
        StaffOrganizationMembership.EnsureAsync(db, cancellationToken);
}

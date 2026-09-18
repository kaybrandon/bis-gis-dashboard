using GisDashboard.Domain;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

/// <summary>
/// QC03 — Editor and Administrator accounts are members of every organization.
/// Assigned techs stay independent (QC01). Viewer/Uploader assignment is unchanged.
/// </summary>
public static class StaffOrganizationMembership
{
    public static async Task EnsureAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var staffUserIds = await StaffUserIdsAsync(db, cancellationToken);
        if (staffUserIds.Count == 0)
        {
            return;
        }

        var orgIds = await db.Organizations.AsNoTracking()
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (orgIds.Count == 0)
        {
            return;
        }

        var existing = await db.UserOrganizations.AsNoTracking()
            .Where(x => staffUserIds.Contains(x.UserId))
            .Select(x => new { x.UserId, x.OrganizationId })
            .ToListAsync(cancellationToken);
        var have = existing.Select(x => (x.UserId, x.OrganizationId)).ToHashSet();
        foreach (var userId in staffUserIds)
        {
            foreach (var orgId in orgIds)
            {
                if (have.Add((userId, orgId)))
                {
                    db.UserOrganizations.Add(new UserOrganization
                    {
                        UserId = userId,
                        OrganizationId = orgId
                    });
                }
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public static async Task AssociateWithOrganizationAsync(
        AppDbContext db,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var staffUserIds = await StaffUserIdsAsync(db, cancellationToken);
        if (staffUserIds.Count == 0)
        {
            return;
        }

        var existing = await db.UserOrganizations.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && staffUserIds.Contains(x.UserId))
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);
        var have = existing.ToHashSet();
        foreach (var userId in staffUserIds)
        {
            if (have.Add(userId))
            {
                db.UserOrganizations.Add(new UserOrganization
                {
                    UserId = userId,
                    OrganizationId = organizationId
                });
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<List<Guid>> StaffUserIdsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var staffRoleIds = await db.Roles.AsNoTracking()
            .Where(x => x.Name == Roles.Editor || x.Name == Roles.Administrator)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (staffRoleIds.Count == 0)
        {
            return [];
        }

        return await db.UserRoles.AsNoTracking()
            .Where(x => staffRoleIds.Contains(x.RoleId))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}

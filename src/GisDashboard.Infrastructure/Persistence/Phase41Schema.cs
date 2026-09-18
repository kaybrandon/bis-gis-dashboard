using GisDashboard.Domain;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

/// <summary>
/// Users → Organizations writes UserOrganizations. Org Assigned tech(s) writes
/// OrganizationTechs. Partial staff membership still backfills Assigned tech.
/// QC03 all-org staff membership is access only and is not promoted to techs.
/// </summary>
public static class Phase41Schema
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var staffRoleIds = await db.Roles.AsNoTracking()
            .Where(x => x.Name == Roles.Editor || x.Name == Roles.Administrator || x.Name == Roles.GlobalAdministrator)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (staffRoleIds.Count == 0)
        {
            return;
        }

        var staffUserIds = await db.UserRoles.AsNoTracking()
            .Where(x => staffRoleIds.Contains(x.RoleId))
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);
        var gaRoleId = await db.Roles.AsNoTracking()
            .Where(x => x.Name == Roles.GlobalAdministrator)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var gaUserIds = gaRoleId == Guid.Empty
            ? []
            : await db.UserRoles.AsNoTracking()
                .Where(x => x.RoleId == gaRoleId)
                .Select(x => x.UserId)
                .ToListAsync(cancellationToken);

        var memberships = await db.UserOrganizations.AsNoTracking()
            .Where(x => staffUserIds.Contains(x.UserId))
            .Select(x => new { x.UserId, x.OrganizationId })
            .ToListAsync(cancellationToken);
        var techs = await db.OrganizationTechs.AsNoTracking()
            .Select(x => new { x.UserId, x.OrganizationId })
            .ToListAsync(cancellationToken);
        var allOrgIds = await db.Organizations.AsNoTracking()
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var membershipCountByUser = memberships
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.OrganizationId).Distinct().Count());

        var techKeys = techs.Select(x => (x.UserId, x.OrganizationId)).ToHashSet();
        foreach (var row in memberships)
        {
            // QC03 — all-org staff membership is access, not Assigned tech. Only promote
            // leftover partial memberships from the pre-QC03 Users → Organizations picker.
            if (allOrgIds.Count > 0
                && membershipCountByUser.GetValueOrDefault(row.UserId) >= allOrgIds.Count)
            {
                continue;
            }

            if (techKeys.Add((row.UserId, row.OrganizationId)))
            {
                db.OrganizationTechs.Add(new OrganizationTech
                {
                    UserId = row.UserId,
                    OrganizationId = row.OrganizationId
                });
            }
        }

        var memberKeys = (await db.UserOrganizations.AsNoTracking()
                .Select(x => new { x.UserId, x.OrganizationId })
                .ToListAsync(cancellationToken))
            .Select(x => (x.UserId, x.OrganizationId))
            .ToHashSet();
        foreach (var row in techs)
        {
            if (gaUserIds.Contains(row.UserId))
            {
                continue;
            }

            if (memberKeys.Add((row.UserId, row.OrganizationId)))
            {
                db.UserOrganizations.Add(new UserOrganization
                {
                    UserId = row.UserId,
                    OrganizationId = row.OrganizationId
                });
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}

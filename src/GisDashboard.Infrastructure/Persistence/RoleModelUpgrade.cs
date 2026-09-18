using GisDashboard.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

/// <summary>
/// Moves Phase 1 Administrator (all-orgs) → GlobalAdministrator and Client → org Administrator.
/// QC04 adds Uploader if missing. Does not migrate existing Viewers.
/// Safe to run on every startup.
/// </summary>
public static class RoleModelUpgrade
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await db.Database.CanConnectAsync(cancellationToken))
        {
            return;
        }

        List<IdentityRole<Guid>> roles;
        try
        {
            roles = await db.Roles.ToListAsync(cancellationToken);
        }
        catch (Exception)
        {
            return;
        }
        var client = roles.FirstOrDefault(x => x.Name == Roles.LegacyClient);
        var orgAdmin = roles.FirstOrDefault(x => x.Name == Roles.Administrator);
        var global = roles.FirstOrDefault(x => x.Name == Roles.GlobalAdministrator);
        var uploader = roles.FirstOrDefault(x => x.Name == Roles.Uploader);

        if (global is null)
        {
            global = new IdentityRole<Guid>(Roles.GlobalAdministrator)
            {
                Id = Guid.NewGuid(),
                NormalizedName = Roles.GlobalAdministrator.ToUpperInvariant(),
                ConcurrencyStamp = Guid.NewGuid().ToString()
            };
            db.Roles.Add(global);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (orgAdmin is null)
        {
            orgAdmin = new IdentityRole<Guid>(Roles.Administrator)
            {
                Id = Guid.NewGuid(),
                NormalizedName = Roles.Administrator.ToUpperInvariant(),
                ConcurrencyStamp = Guid.NewGuid().ToString()
            };
            db.Roles.Add(orgAdmin);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (uploader is null)
        {
            db.Roles.Add(new IdentityRole<Guid>(Roles.Uploader)
            {
                Id = Guid.NewGuid(),
                NormalizedName = Roles.Uploader.ToUpperInvariant(),
                ConcurrencyStamp = Guid.NewGuid().ToString()
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        if (client is not null)
        {
            // Old 4-role model: everyone on Administrator was the all-orgs admin.
            if (!await db.UserRoles.AnyAsync(x => x.RoleId == global.Id, cancellationToken))
            {
                var formerGlobals = await db.UserRoles
                    .Where(x => x.RoleId == orgAdmin.Id)
                    .ToListAsync(cancellationToken);
                foreach (var link in formerGlobals)
                {
                    db.UserRoles.Remove(link);
                    db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = link.UserId, RoleId = global.Id });
                }
            }

            var formerClients = await db.UserRoles
                .Where(x => x.RoleId == client.Id)
                .ToListAsync(cancellationToken);
            foreach (var link in formerClients)
            {
                db.UserRoles.Remove(link);
                if (!await db.UserRoles.AnyAsync(x => x.UserId == link.UserId && x.RoleId == orgAdmin.Id, cancellationToken))
                {
                    db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = link.UserId, RoleId = orgAdmin.Id });
                }
            }

            db.Roles.Remove(client);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}

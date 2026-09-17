using GisDashboard.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

/// <summary>
/// One-time identity cleanup: person-like DisplayName values become FullName,
/// and Identity UserName becomes first-initial + last (or email local-part).
/// Idempotent — only rewrites usernames that are still emails.
/// </summary>
public static class Phase42Schema
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var users = await db.Users.ToListAsync(cancellationToken);
        if (users.Count == 0)
        {
            return;
        }

        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var user in users)
        {
            if (!UserIdentity.NeedsUsernameRewrite(user.UserName, user.Email))
            {
                var current = UserIdentity.NormalizeUsername(user.UserName);
                if (!string.IsNullOrEmpty(current))
                {
                    taken.Add(current);
                }
            }
        }

        foreach (var user in users)
        {
            if (user.Id == SeedIds.TokenUploadUser)
            {
                continue;
            }

            var originalLabel = !UserIdentity.LooksLikeEmail(user.DisplayName)
                ? user.DisplayName
                : user.UserName;
            if (string.IsNullOrWhiteSpace(user.FullName) && UserIdentity.LooksLikePersonName(originalLabel) && originalLabel is not null)
            {
                user.FullName = UserIdentity.ToTitleCase(originalLabel);
            }

            if (!UserIdentity.NeedsUsernameRewrite(user.UserName, user.Email))
            {
                if (string.IsNullOrWhiteSpace(user.DisplayName) || UserIdentity.LooksLikeEmail(user.DisplayName))
                {
                    user.DisplayName = user.UserName ?? user.DisplayName;
                }

                continue;
            }

            string seed;
            if (user.Id == SeedIds.Admin
                || string.Equals(user.Email, "admin@bisconsultants.local", StringComparison.OrdinalIgnoreCase))
            {
                seed = "admin";
            }
            else if (UserIdentity.LooksLikePersonName(user.FullName))
            {
                seed = UserIdentity.FromPersonName(user.FullName!);
            }
            else if (UserIdentity.LooksLikePersonName(originalLabel))
            {
                seed = UserIdentity.FromPersonName(originalLabel!);
            }
            else
            {
                seed = UserIdentity.FromEmail(user.Email);
            }

            var username = UserIdentity.UniqueUsername(seed, taken);
            taken.Add(username);
            user.UserName = username;
            user.NormalizedUserName = username.ToUpperInvariant();
            user.DisplayName = username;
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}

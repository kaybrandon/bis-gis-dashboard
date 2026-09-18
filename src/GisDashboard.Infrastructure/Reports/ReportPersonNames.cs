using GisDashboard.Application.Abstractions;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Reports;

/// <summary>
/// GIS-UI-07 — Full name on send-report / export person labels when it is on the user record.
/// </summary>
internal static class ReportPersonNames
{
    public static string FromUser(ApplicationUser user) =>
        UserIdentity.ReportName(user.FullName, user.DisplayName, user.UserName, user.Email);

    public static string FromFields(string? fullName, string? displayName, string? userName = null, string? email = null) =>
        UserIdentity.ReportName(fullName, displayName, userName, email);

    public static string? FullNameOrNull(string? fullName) =>
        string.IsNullOrWhiteSpace(fullName) ? null : fullName.Trim();

    public static async Task<string> ForCurrentUserAsync(
        AppDbContext db,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == currentUser.UserId)
            .Select(u => new { u.FullName, u.DisplayName, u.UserName, u.Email })
            .FirstOrDefaultAsync(cancellationToken);
        return user is null
            ? currentUser.DisplayName
            : FromFields(user.FullName, user.DisplayName, user.UserName, user.Email);
    }
}

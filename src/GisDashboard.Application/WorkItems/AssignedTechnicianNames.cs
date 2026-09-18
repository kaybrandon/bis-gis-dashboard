using GisDashboard.Domain;

namespace GisDashboard.Application.WorkItems;

public static class AssignedTechnicianNames
{
    public static IReadOnlyList<AssignedTechnicianDisplay> FromUsers(
        IEnumerable<(string? FullName, string? DisplayName, string? UserName, string? Email, bool IsPrimary, bool IsArchived)> rows)
    {
        var all = rows
            .Select(x => new AssignedTechnicianDisplay(
                UserIdentity.WithArchivedSuffix(
                    UserIdentity.ShortPublicName(x.FullName, x.DisplayName, x.UserName, x.Email),
                    x.IsArchived),
                x.IsPrimary))
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
        var primary = all.Where(x => x.IsPrimary).ToList();
        return primary.Count > 0 ? primary : all;
    }
}

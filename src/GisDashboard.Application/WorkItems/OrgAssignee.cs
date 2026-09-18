namespace GisDashboard.Application.WorkItems;

/// <summary>
/// CR08 — new uploads map to the org Assigned technician list (QC01), not historical
/// work-item Assigned to. Primary wins; otherwise the first sign-in-capable tech.
/// </summary>
public static class OrgAssignee
{
    public static Guid? ResolveDefault(
        IEnumerable<(Guid UserId, string DisplayName, bool IsPrimary, bool CanSignIn)> techs)
    {
        return techs
            .Where(x => x.CanSignIn)
            .OrderBy(x => x.IsPrimary ? 0 : 1)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(x => (Guid?)x.UserId)
            .FirstOrDefault();
    }
}

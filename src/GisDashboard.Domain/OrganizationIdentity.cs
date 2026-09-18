namespace GisDashboard.Domain;

public static class OrganizationIdentity
{
    public static string HistoricalName(string? name, bool isArchived) =>
        UserIdentity.WithArchivedSuffix(name, isArchived);
}

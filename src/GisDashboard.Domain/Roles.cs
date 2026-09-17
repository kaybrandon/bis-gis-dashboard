namespace GisDashboard.Domain;

public static class Roles
{
    public const string GlobalAdministrator = "GlobalAdministrator";
    public const string Administrator = "Administrator";
    public const string Editor = "Editor";
    public const string Viewer = "Viewer";

    /// <summary>Legacy Phase 1 role name. Migrated to org Administrator. Not assignable.</summary>
    public const string LegacyClient = "Client";

    public static readonly string[] All =
    [
        GlobalAdministrator,
        Administrator,
        Editor,
        Viewer
    ];

    public const string DirectoryManagers = $"{GlobalAdministrator},{Administrator}";

    public static string DisplayName(string role) => role switch
    {
        GlobalAdministrator => "Global Administrator",
        Administrator => "Administrator",
        Editor => "Editor",
        Viewer => "Viewer",
        _ => role
    };

    public static bool IsGlobalAdmin(string role) => role == GlobalAdministrator;

    public static bool IsOrgAdmin(string role) => role == Administrator;

    public static bool CanSeeInternalNotes(string role) =>
        role is GlobalAdministrator or Administrator or Editor;

    public static bool CanPostComments(string role) =>
        role is GlobalAdministrator or Administrator or Editor or Viewer;

    public static bool CanEditInternalNotes(string role) =>
        role is GlobalAdministrator or Administrator or Editor;

    public static bool CanSeeTimeLogs(string role) =>
        role is GlobalAdministrator or Administrator or Editor or Viewer;

    public static bool CanLogTime(string role) =>
        role is GlobalAdministrator or Administrator or Editor;

    public static bool CanSeeAllOrganizations(string role) =>
        role is GlobalAdministrator or Administrator or Editor;

    public static bool CanUpload(string role) =>
        role is GlobalAdministrator or Administrator or Editor or Viewer;

    public static bool CanMutateWorkItems(string role) =>
        role is GlobalAdministrator or Administrator or Editor;

    public static bool CanManageDirectory(string role) =>
        role is GlobalAdministrator or Administrator;

    public static bool CanManageGlobalDirectory(string role) =>
        role is GlobalAdministrator;

    public static bool CanManageTimeBroadly(string role) =>
        role is GlobalAdministrator or Administrator;

    public static bool CanBeAssignedWork(string role) =>
        role is GlobalAdministrator or Administrator or Editor;

    public static bool CanSeePresence(string role) =>
        role is GlobalAdministrator or Administrator or Editor;
}

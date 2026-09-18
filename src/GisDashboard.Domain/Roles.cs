namespace GisDashboard.Domain;

public static class Roles
{
    public const string GlobalAdministrator = "GlobalAdministrator";
    public const string Administrator = "Administrator";
    public const string Editor = "Editor";
    public const string Uploader = "Uploader";
    public const string Viewer = "Viewer";

    /// <summary>Legacy Phase 1 role name. Migrated to org Administrator. Not assignable.</summary>
    public const string LegacyClient = "Client";

    public static readonly string[] All =
    [
        GlobalAdministrator,
        Administrator,
        Editor,
        Uploader,
        Viewer
    ];

    public const string DirectoryManagers = $"{GlobalAdministrator},{Administrator}";

    /// <summary>QC01 — org Assigned tech(s) on Organizations → Edit.</summary>
    public const string AssignedTechManagers = $"{GlobalAdministrator},{Administrator},{Editor}";

    public static string DisplayName(string role) => role switch
    {
        GlobalAdministrator => "Global Administrator",
        Administrator => "Administrator",
        Editor => "Editor",
        Uploader => "Uploader",
        Viewer => "Viewer",
        _ => role
    };

    public static bool IsGlobalAdmin(string role) => role == GlobalAdministrator;

    public static bool IsOrgAdmin(string role) => role == Administrator;

    /// <summary>QC04 — org-scoped client roles. Viewer is read-only; Uploader can upload and comment.</summary>
    public static bool IsOrgScopedClient(string role) =>
        role is Viewer or Uploader;

    /// <summary>
    /// QC03 — Editor and Administrator are persisted as members of every organization.
    /// Global Administrator keeps implicit all-org access (WL01) without membership rows.
    /// </summary>
    public static bool ReceivesAllOrganizationMembership(string role) =>
        role is Administrator or Editor;

    /// <summary>QC03 / QC04 — Viewer and Uploader stay limited to explicitly assigned orgs.</summary>
    public static bool RequiresAssignedOrganizations(string role) =>
        role is Viewer or Uploader;

    public static bool CanSeeInternalNotes(string role) =>
        role is GlobalAdministrator or Administrator or Editor;

    public static bool CanPostComments(string role) =>
        role is GlobalAdministrator or Administrator or Editor or Uploader;

    public static bool CanEditInternalNotes(string role) =>
        role is GlobalAdministrator or Administrator or Editor;

    public static bool CanSeeTimeLogs(string role) =>
        role is GlobalAdministrator or Administrator or Editor or Uploader or Viewer;

    public static bool CanLogTime(string role) =>
        role is GlobalAdministrator or Administrator or Editor;

    public static bool CanSeeAllOrganizations(string role) =>
        role is GlobalAdministrator or Administrator or Editor;

    public static bool CanUpload(string role) =>
        role is GlobalAdministrator or Administrator or Editor or Uploader;

    public static bool CanMutateWorkItems(string role) =>
        role is GlobalAdministrator or Administrator or Editor;

    public static bool CanManageDirectory(string role) =>
        role is GlobalAdministrator or Administrator;

    public static bool CanManageGlobalDirectory(string role) =>
        role is GlobalAdministrator;

    public static bool CanManageAssignedTechs(string role) =>
        role is GlobalAdministrator or Administrator or Editor;

    public static bool CanManageTimeBroadly(string role) =>
        role is GlobalAdministrator or Administrator;

    public static bool CanBeAssignedWork(string role) =>
        role is GlobalAdministrator or Administrator or Editor;

    public static bool CanSeePresence(string role) =>
        role is GlobalAdministrator or Administrator or Editor;

    public static bool CanSeeConnections(string role) =>
        role is GlobalAdministrator or Administrator or Editor;

    public static bool CanManageConnections(string role) =>
        role is GlobalAdministrator or Administrator;

    /// <summary>
    /// QC08 — Dashboard Assignee filter is staff-only. Viewer and Uploader never see it.
    /// Hidden control is not isolation; dashboard and lookup APIs stay assigned-org.
    /// </summary>
    public static bool CanSeeDashboardAssignee(string role) =>
        role is GlobalAdministrator or Administrator or Editor;
}

namespace GisDashboard.Application.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    string Email { get; }
    string DisplayName { get; }
    string Role { get; }
    bool IsGlobalAdmin { get; }
    bool IsOrgAdmin { get; }
    bool IsAdmin { get; }
    bool CanSeeInternalNotes { get; }
    bool CanPostComments { get; }
    bool CanEditInternalNotes { get; }
    bool CanSeeTimeLogs { get; }
    bool CanLogTime { get; }
    bool CanSeeAllOrganizations { get; }
    bool CanUpload { get; }
    bool CanMutateWorkItems { get; }
    bool CanManageDirectory { get; }
    bool CanManageGlobalDirectory { get; }
    bool CanManageAssignedTechs { get; }
    bool CanSeePresence { get; }
    bool CanSeeConnections { get; }
    bool CanManageConnections { get; }
}

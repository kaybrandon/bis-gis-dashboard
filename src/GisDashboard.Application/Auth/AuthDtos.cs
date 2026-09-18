namespace GisDashboard.Application.Auth;

public sealed record LoginRequest(string? Email, string Password, string? UserName = null);

public sealed record AuthUser(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    string RoleDisplayName,
    IReadOnlyList<OrgOption> Organizations,
    bool CanSeeInternalNotes,
    bool CanEditInternalNotes,
    bool CanSeeTimeLogs,
    bool CanLogTime,
    bool CanUpload,
    bool CanMutateWorkItems,
    bool CanManageDirectory,
    bool CanManageGlobalDirectory,
    bool CanManageAssignedTechs,
    bool CanViewTimeReport,
    bool CanViewTeamTimeReport,
    bool CanSeeAllOrganizations,
    string? FullName = null,
    string? WorkPhone = null,
    bool HasAvatar = false,
    string? UserName = null,
    bool CanSeePresence = false,
    bool CanSeeConnections = false,
    bool CanManageConnections = false);

public sealed record UpdateProfileRequest(
    string? UserName,
    string? DisplayName,
    string? FullName,
    string Email,
    string? WorkPhone,
    string? CurrentPassword,
    string? NewPassword,
    string? ConfirmPassword);

public sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt, AuthUser User);

public sealed record OrgOption(Guid Id, string Name, string Code);

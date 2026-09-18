namespace GisDashboard.Application.Directory;

public sealed record OrganizationDto(
    Guid Id,
    string Name,
    string Code,
    bool IsActive,
    DateTimeOffset CreatedAt,
    bool HasUploadToken,
    int? ParcelTotalRealAccounts,
    int? ParcelWithOwnership,
    bool TimeReportCardsVisible,
    IReadOnlyList<AssignedTechDto> AssignedTechs,
    IReadOnlyList<AssignedTechDto> Members);

public sealed record AssignedTechDto(Guid Id, string DisplayName, bool IsPrimary = false);

public sealed record UpdateOrganizationRequest(
    string Name,
    string? Code,
    bool? IsActive,
    int? ParcelTotalRealAccounts,
    int? ParcelWithOwnership,
    bool? TimeReportCardsVisible,
    IReadOnlyList<Guid>? AssignedTechIds,
    Guid? PrimaryAssignedTechId = null);

public sealed record UpdateUserRequest(
    string DisplayName,
    string Email,
    string Role,
    IReadOnlyList<Guid>? OrganizationIds,
    bool IsActive,
    string? Password,
    string? FullName = null,
    string? WorkPhone = null,
    string? Title = null);

public sealed record UploadLinkDto(
    Guid OrganizationId,
    string OrganizationName,
    string Token,
    string Path,
    DateTimeOffset? CreatedAt);

public sealed record UserListItem(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    bool IsActive,
    IReadOnlyList<OrgMember> Organizations,
    string? FullName = null,
    string? WorkPhone = null,
    bool HasAvatar = false,
    DateTimeOffset? LastLoginAt = null,
    bool IsArchived = false,
    DateTimeOffset? ArchivedAt = null,
    string? Title = null);

public sealed record OrgMember(Guid OrganizationId, string OrganizationName);

public sealed record CreateOrganizationRequest(string Name, string Code);

public sealed record CreateUserRequest(
    string Email,
    string Password,
    string DisplayName,
    string Role,
    IReadOnlyList<Guid>? OrganizationIds = null,
    string? FullName = null,
    string? WorkPhone = null,
    string? Title = null);

public sealed record UpdateUserOrgsRequest(IReadOnlyList<Guid>? OrganizationIds);

public sealed record LookupItem(Guid Id, string Name, string? Color, int SortOrder);

public sealed record AssignableUser(Guid Id, string DisplayName, string Email);

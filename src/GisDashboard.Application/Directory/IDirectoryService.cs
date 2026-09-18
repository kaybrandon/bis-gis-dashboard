using GisDashboard.Application.Auth;
using GisDashboard.Application.WorkItems;

namespace GisDashboard.Application.Directory;

public interface IDirectoryService
{
    Task<IReadOnlyList<OrganizationDto>> ListOrganizationsAsync(CancellationToken cancellationToken = default);
    Task<OrganizationDto> CreateOrganizationAsync(CreateOrganizationRequest request, CancellationToken cancellationToken = default);
    Task<OrganizationDto> UpdateOrganizationAsync(Guid id, UpdateOrganizationRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserListItem>> ListUsersAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<UserListItem> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserListItem> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserListItem> UpdateUserOrganizationsAsync(Guid userId, UpdateUserOrgsRequest request, CancellationToken cancellationToken = default);
    Task<UserListItem> ArchiveUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserListItem> RestoreUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UploadLinkDto> GetUploadLinkAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<UploadLinkDto> RegenerateUploadLinkAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupItem>> ListDocumentTypesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupItem>> ListStatusesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrgOption>> ListAccessibleOrganizationsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssignableUser>> ListAssignableUsersAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssignedTechnicianDisplay>> ListAssignedTechniciansAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssignableUser>> ListScopedAssigneesAsync(CancellationToken cancellationToken = default);
    Task<GisDashboard.Application.WorkItems.StatusActions> GetStatusActionsAsync(CancellationToken cancellationToken = default);
}

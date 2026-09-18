using GisDashboard.Application.Auth;
using GisDashboard.Application.Directory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GisDashboard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class DirectoryController : ControllerBase
{
    private readonly IDirectoryService _directory;

    public DirectoryController(IDirectoryService directory)
    {
        _directory = directory;
    }

    [HttpGet("lookups/document-types")]
    public Task<IReadOnlyList<LookupItem>> DocumentTypes(CancellationToken cancellationToken) =>
        _directory.ListDocumentTypesAsync(cancellationToken);

    [HttpGet("lookups/statuses")]
    public Task<IReadOnlyList<LookupItem>> Statuses(CancellationToken cancellationToken) =>
        _directory.ListStatusesAsync(cancellationToken);

    [HttpGet("lookups/organizations")]
    public Task<IReadOnlyList<OrgOption>> AccessibleOrganizations(CancellationToken cancellationToken) =>
        _directory.ListAccessibleOrganizationsAsync(cancellationToken);

    [HttpGet("lookups/assignable-users")]
    public Task<IReadOnlyList<AssignableUser>> AssignableUsers([FromQuery] Guid organizationId, CancellationToken cancellationToken) =>
        _directory.ListAssignableUsersAsync(organizationId, cancellationToken);

    [HttpGet("lookups/assigned-technicians")]
    public Task<IReadOnlyList<GisDashboard.Application.WorkItems.AssignedTechnicianDisplay>> AssignedTechnicians(
        [FromQuery] Guid organizationId,
        CancellationToken cancellationToken) =>
        _directory.ListAssignedTechniciansAsync(organizationId, cancellationToken);

    [HttpGet("lookups/assignees")]
    public Task<IReadOnlyList<AssignableUser>> Assignees(CancellationToken cancellationToken) =>
        _directory.ListScopedAssigneesAsync(cancellationToken);

    [HttpGet("lookups/status-actions")]
    public Task<GisDashboard.Application.WorkItems.StatusActions> StatusActions(CancellationToken cancellationToken) =>
        _directory.GetStatusActionsAsync(cancellationToken);

    [Authorize(Roles = Domain.Roles.AssignedTechManagers)]
    [HttpGet("admin/organizations")]
    public Task<IReadOnlyList<OrganizationDto>> AdminOrganizations(CancellationToken cancellationToken) =>
        _directory.ListOrganizationsAsync(cancellationToken);

    [Authorize(Roles = Domain.Roles.GlobalAdministrator)]
    [HttpPost("admin/organizations")]
    public Task<OrganizationDto> CreateOrganization([FromBody] CreateOrganizationRequest request, CancellationToken cancellationToken) =>
        _directory.CreateOrganizationAsync(request, cancellationToken);

    [Authorize(Roles = Domain.Roles.AssignedTechManagers)]
    [HttpPut("admin/organizations/{id:guid}")]
    public Task<OrganizationDto> UpdateOrganization(Guid id, [FromBody] UpdateOrganizationRequest request, CancellationToken cancellationToken) =>
        _directory.UpdateOrganizationAsync(id, request, cancellationToken);

    [Authorize(Roles = Domain.Roles.DirectoryManagers)]
    [HttpGet("admin/organizations/{id:guid}/upload-link")]
    public Task<UploadLinkDto> GetUploadLink(Guid id, CancellationToken cancellationToken) =>
        _directory.GetUploadLinkAsync(id, cancellationToken);

    [Authorize(Roles = Domain.Roles.DirectoryManagers)]
    [HttpPost("admin/organizations/{id:guid}/upload-link/regenerate")]
    public Task<UploadLinkDto> RegenerateUploadLink(Guid id, CancellationToken cancellationToken) =>
        _directory.RegenerateUploadLinkAsync(id, cancellationToken);

    [Authorize(Roles = Domain.Roles.DirectoryManagers)]
    [HttpGet("admin/users")]
    public Task<IReadOnlyList<UserListItem>> Users([FromQuery] bool includeArchived = false, CancellationToken cancellationToken = default) =>
        _directory.ListUsersAsync(includeArchived, cancellationToken);

    [Authorize(Roles = Domain.Roles.DirectoryManagers)]
    [HttpPost("admin/users")]
    public Task<UserListItem> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken) =>
        _directory.CreateUserAsync(request, cancellationToken);

    [Authorize(Roles = Domain.Roles.DirectoryManagers)]
    [HttpPut("admin/users/{id:guid}")]
    public Task<UserListItem> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken) =>
        _directory.UpdateUserAsync(id, request, cancellationToken);

    [Authorize(Roles = Domain.Roles.DirectoryManagers)]
    [HttpPost("admin/users/{id:guid}/archive")]
    public Task<UserListItem> ArchiveUser(Guid id, CancellationToken cancellationToken) =>
        _directory.ArchiveUserAsync(id, cancellationToken);

    [Authorize(Roles = Domain.Roles.DirectoryManagers)]
    [HttpPost("admin/users/{id:guid}/restore")]
    public Task<UserListItem> RestoreUser(Guid id, CancellationToken cancellationToken) =>
        _directory.RestoreUserAsync(id, cancellationToken);

    [Authorize(Roles = Domain.Roles.DirectoryManagers)]
    [HttpPut("admin/users/{id:guid}/organizations")]
    public Task<UserListItem> UpdateUserOrgs(Guid id, [FromBody] UpdateUserOrgsRequest request, CancellationToken cancellationToken) =>
        _directory.UpdateUserOrganizationsAsync(id, request, cancellationToken);
}

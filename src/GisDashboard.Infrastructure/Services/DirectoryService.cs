using GisDashboard.Application.Abstractions;
using GisDashboard.Application.Auth;
using GisDashboard.Application.Directory;
using GisDashboard.Application.WorkItems;
using GisDashboard.Application.Exceptions;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;
using GisDashboard.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Services;

public sealed class DirectoryService : IDirectoryService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ICurrentUser _currentUser;
    private readonly IOrgScope _orgScope;

    public DirectoryService(
        AppDbContext db,
        UserManager<ApplicationUser> users,
        ICurrentUser currentUser,
        IOrgScope orgScope)
    {
        _db = db;
        _users = users;
        _currentUser = currentUser;
        _orgScope = orgScope;
    }

    public async Task<IReadOnlyList<OrganizationDto>> ListOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        EnsureDirectoryManager();
        var allowed = await _orgScope.GetAllowedOrganizationIdsAsync(cancellationToken);
        var orgs = await _db.Organizations.AsNoTracking()
            .Include(x => x.AssignedTechs)
            .ThenInclude(x => x.User)
            .Include(x => x.Members)
            .ThenInclude(x => x.User)
            .Where(x => allowed.Contains(x.Id))
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return orgs.Select(MapOrg).ToList();
    }

    public async Task<OrganizationDto> CreateOrganizationAsync(CreateOrganizationRequest request, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.CanManageGlobalDirectory)
        {
            throw new ForbiddenException("Only a Global Administrator can create organizations.");
        }

        var name = (request.Name ?? string.Empty).Trim();
        var code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        {
            throw new ValidationException("Organization name and code are required.");
        }

        if (await _db.Organizations.AnyAsync(x => x.Code == code, cancellationToken))
        {
            throw new ConflictException("An organization with that code already exists.");
        }

        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = name,
            Code = code,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UploadToken = UploadTokens.Create(),
            UploadTokenCreatedAt = DateTimeOffset.UtcNow
        };
        _db.Organizations.Add(org);
        await _db.SaveChangesAsync(cancellationToken);
        return MapOrg(org);
    }

    public async Task<OrganizationDto> UpdateOrganizationAsync(Guid id, UpdateOrganizationRequest request, CancellationToken cancellationToken = default)
    {
        EnsureDirectoryManager();
        var org = await LoadScopedOrganizationAsync(id, cancellationToken);
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException("Organization name is required.");
        }

        org.Name = name;

        if (_currentUser.CanManageGlobalDirectory)
        {
            if (!string.IsNullOrWhiteSpace(request.Code))
            {
                var code = request.Code.Trim().ToUpperInvariant();
                if (await _db.Organizations.AnyAsync(x => x.Code == code && x.Id != id, cancellationToken))
                {
                    throw new ConflictException("An organization with that code already exists.");
                }

                org.Code = code;
            }

            if (request.IsActive is { } active)
            {
                org.IsActive = active;
            }
        }

        ApplyParcelInventory(org, request.ParcelTotalRealAccounts, request.ParcelWithOwnership);

        if (_currentUser.CanManageGlobalDirectory && request.TimeReportCardsVisible is { } visible)
        {
            org.TimeReportCardsVisible = visible;
        }

        if (_currentUser.CanManageGlobalDirectory && request.AssignedTechIds is not null)
        {
            await ReplaceAssignedTechsAsync(org, request.AssignedTechIds, request.PrimaryAssignedTechId, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return MapOrg(await LoadOrganizationGraphAsync(org.Id, cancellationToken));
    }

    public async Task<IReadOnlyList<UserListItem>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        EnsureDirectoryManager();
        var allowed = await _orgScope.GetAllowedOrganizationIdsAsync(cancellationToken);
        var users = await _db.Users.AsNoTracking()
            .Include(x => x.Organizations)
            .ThenInclude(x => x.Organization)
            .OrderBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);

        var result = new List<UserListItem>();
        foreach (var user in users)
        {
            var roles = await _users.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? string.Empty;
            if (!_currentUser.IsGlobalAdmin)
            {
                var inScope = user.Organizations.Any(x => allowed.Contains(x.OrganizationId));
                if (!inScope)
                {
                    continue;
                }
            }

            result.Add(MapUser(user, role));
        }

        return result;
    }

    public async Task<UserListItem> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        EnsureDirectoryManager();
        if (!Roles.All.Contains(request.Role))
        {
            throw new ValidationException("Role is not valid.");
        }

        if (Roles.IsGlobalAdmin(request.Role) && !_currentUser.CanManageGlobalDirectory)
        {
            throw new ForbiddenException("Only a Global Administrator can create a Global Administrator.");
        }

        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ValidationException("Email and password are required.");
        }

        var orgIds = await NormalizeOrgIdsAsync(request.Role, request.OrganizationIds, cancellationToken);
        var label = (request.DisplayName ?? string.Empty).Trim();
        var fullName = string.IsNullOrWhiteSpace(request.FullName)
            ? (UserIdentity.LooksLikePersonName(label) ? UserIdentity.ToTitleCase(label) : null)
            : request.FullName.Trim();
        var username = await UniqueUsernameAsync(Guid.Empty, label, email, fullName, cancellationToken);
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = username,
            Email = email,
            EmailConfirmed = true,
            DisplayName = username,
            FullName = fullName,
            WorkPhone = string.IsNullOrWhiteSpace(request.WorkPhone) ? null : request.WorkPhone.Trim(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var created = await _users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            throw new ValidationException(string.Join(" ", created.Errors.Select(e => e.Description)));
        }

        await _users.AddToRoleAsync(user, request.Role);
        await ReplaceUserOrganizationsAsync(user.Id, request.Role, orgIds, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        user = await _db.Users.Include(x => x.Organizations).ThenInclude(x => x.Organization)
            .FirstAsync(x => x.Id == user.Id, cancellationToken);
        return MapUser(user, request.Role);
    }

    public async Task<UserListItem> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        EnsureDirectoryManager();
        var user = await LoadScopedUserAsync(userId, cancellationToken);
        var currentRole = (await _users.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty;
        if (!Roles.All.Contains(request.Role))
        {
            throw new ValidationException("Role is not valid.");
        }

        if (Roles.IsGlobalAdmin(request.Role) && !_currentUser.CanManageGlobalDirectory)
        {
            throw new ForbiddenException("Only a Global Administrator can assign Global Administrator.");
        }

        if (Roles.IsGlobalAdmin(currentRole) && !_currentUser.IsGlobalAdmin)
        {
            throw new NotFoundException("User was not found.");
        }

        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        var displayName = (request.DisplayName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(displayName))
        {
            throw new ValidationException("Username and email are required.");
        }

        var fullName = string.IsNullOrWhiteSpace(request.FullName)
            ? (UserIdentity.LooksLikePersonName(displayName) ? UserIdentity.ToTitleCase(displayName) : user.FullName)
            : request.FullName.Trim();
        var username = await UniqueUsernameAsync(userId, displayName, email, fullName, cancellationToken);

        if (await _users.FindByEmailAsync(email) is { } other && other.Id != userId)
        {
            throw new ConflictException("A user with that email already exists.");
        }

        user.DisplayName = username;
        user.FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName;
        user.WorkPhone = string.IsNullOrWhiteSpace(request.WorkPhone) ? null : request.WorkPhone.Trim();
        user.IsActive = request.IsActive;
        var emailResult = await _users.SetEmailAsync(user, email);
        if (!emailResult.Succeeded)
        {
            throw new ValidationException(string.Join(" ", emailResult.Errors.Select(e => e.Description)));
        }

        var nameResult = await _users.SetUserNameAsync(user, username);
        if (!nameResult.Succeeded)
        {
            throw new ValidationException(string.Join(" ", nameResult.Errors.Select(e => e.Description)));
        }

        if (currentRole != request.Role)
        {
            if (!string.IsNullOrWhiteSpace(currentRole))
            {
                await _users.RemoveFromRoleAsync(user, currentRole);
            }

            await _users.AddToRoleAsync(user, request.Role);
        }

        var orgIds = await NormalizeOrgIdsAsync(request.Role, request.OrganizationIds, cancellationToken);
        await ReplaceUserOrganizationsAsync(userId, request.Role, orgIds, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            var token = await _users.GeneratePasswordResetTokenAsync(user);
            var reset = await _users.ResetPasswordAsync(user, token, request.Password);
            if (!reset.Succeeded)
            {
                throw new ValidationException(string.Join(" ", reset.Errors.Select(e => e.Description)));
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        user = await _db.Users.Include(x => x.Organizations).ThenInclude(x => x.Organization)
            .FirstAsync(x => x.Id == userId, cancellationToken);
        return MapUser(user, request.Role);
    }

    public async Task<UserListItem> UpdateUserOrganizationsAsync(Guid userId, UpdateUserOrgsRequest request, CancellationToken cancellationToken = default)
    {
        EnsureDirectoryManager();
        var user = await _db.Users
            .Include(x => x.Organizations)
            .ThenInclude(x => x.Organization)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        var role = (await _users.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty;
        if (Roles.IsGlobalAdmin(role) && !_currentUser.IsGlobalAdmin)
        {
            throw new NotFoundException("User was not found.");
        }

        if (!_currentUser.IsGlobalAdmin)
        {
            var allowed = await _orgScope.GetAllowedOrganizationIdsAsync(cancellationToken);
            if (!user.Organizations.Any(x => allowed.Contains(x.OrganizationId)))
            {
                throw new NotFoundException("User was not found.");
            }
        }

        var orgIds = await NormalizeOrgIdsAsync(role, request.OrganizationIds, cancellationToken);
        await ReplaceUserOrganizationsAsync(userId, role, orgIds, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        user = await _db.Users.Include(x => x.Organizations).ThenInclude(x => x.Organization)
            .FirstAsync(x => x.Id == userId, cancellationToken);
        return MapUser(user, role);
    }

    public async Task<IReadOnlyList<LookupItem>> ListDocumentTypesAsync(CancellationToken cancellationToken = default) =>
        await _db.DocumentTypes.AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .Select(x => new LookupItem(x.Id, x.Name, null, x.SortOrder))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LookupItem>> ListStatusesAsync(CancellationToken cancellationToken = default) =>
        await _db.WorkItemStatuses.AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .Select(x => new LookupItem(x.Id, x.Name, x.Color, x.SortOrder))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OrgOption>> ListAccessibleOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        var allowed = await _orgScope.GetAllowedOrganizationIdsAsync(cancellationToken);
        return await _db.Organizations.AsNoTracking()
            .Where(x => allowed.Contains(x.Id))
            .OrderBy(x => x.Name)
            .Select(x => new OrgOption(x.Id, x.Name, x.Code))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AssignableUser>> ListAssignableUsersAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        await _orgScope.EnsureCanAccessOrganizationAsync(organizationId, cancellationToken);
        if (!_currentUser.CanMutateWorkItems)
        {
            return [];
        }

        var globalRoleId = await _db.Roles.Where(x => x.Name == Roles.GlobalAdministrator).Select(x => x.Id).FirstAsync(cancellationToken);
        var adminRoleId = await _db.Roles.Where(x => x.Name == Roles.Administrator).Select(x => x.Id).FirstAsync(cancellationToken);
        var editorRoleId = await _db.Roles.Where(x => x.Name == Roles.Editor).Select(x => x.Id).FirstAsync(cancellationToken);

        return await _db.Users.AsNoTracking()
            .Where(u => u.IsActive && _db.UserRoles.Any(ur =>
                ur.UserId == u.Id &&
                (ur.RoleId == globalRoleId || ur.RoleId == adminRoleId || ur.RoleId == editorRoleId)))
            .OrderBy(u => u.FullName ?? u.DisplayName)
            .Select(u => new AssignableUser(
                u.Id,
                u.FullName != null && u.FullName != "" ? u.FullName : u.DisplayName,
                u.Email ?? string.Empty))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AssignedTechnicianDisplay>> ListAssignedTechniciansAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        await _orgScope.EnsureCanAccessOrganizationAsync(organizationId, cancellationToken);
        var rows = await _db.OrganizationTechs.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => new
            {
                x.User.FullName,
                x.User.DisplayName,
                x.User.UserName,
                x.User.Email,
                x.IsPrimary
            })
            .ToListAsync(cancellationToken);
        return AssignedTechnicianNames.FromUsers(
            rows.Select(x => (x.FullName, x.DisplayName, x.UserName, (string?)x.Email, x.IsPrimary)));
    }

    public async Task<IReadOnlyList<AssignableUser>> ListScopedAssigneesAsync(CancellationToken cancellationToken = default)
    {
        var allowed = await _orgScope.GetAllowedOrganizationIdsAsync(cancellationToken);
        var globalRoleId = await _db.Roles.Where(x => x.Name == Roles.GlobalAdministrator).Select(x => x.Id).FirstAsync(cancellationToken);
        var adminRoleId = await _db.Roles.Where(x => x.Name == Roles.Administrator).Select(x => x.Id).FirstAsync(cancellationToken);
        var editorRoleId = await _db.Roles.Where(x => x.Name == Roles.Editor).Select(x => x.Id).FirstAsync(cancellationToken);

        _ = allowed;
        return await _db.Users.AsNoTracking()
            .Where(u => u.IsActive && _db.UserRoles.Any(ur =>
                ur.UserId == u.Id &&
                (ur.RoleId == globalRoleId || ur.RoleId == adminRoleId || ur.RoleId == editorRoleId)))
            .OrderBy(u => u.FullName ?? u.DisplayName)
            .Select(u => new AssignableUser(
                u.Id,
                u.FullName != null && u.FullName != "" ? u.FullName : u.DisplayName,
                u.Email ?? string.Empty))
            .ToListAsync(cancellationToken);
    }

    public async Task<UploadLinkDto> GetUploadLinkAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var org = await LoadScopedOrganizationAsync(organizationId, cancellationToken);
        if (string.IsNullOrWhiteSpace(org.UploadToken))
        {
            org.UploadToken = UploadTokens.Create();
            org.UploadTokenCreatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return MapLink(org);
    }

    public async Task<UploadLinkDto> RegenerateUploadLinkAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var org = await LoadScopedOrganizationAsync(organizationId, cancellationToken);
        org.UploadToken = UploadTokens.Create();
        org.UploadTokenCreatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapLink(org);
    }

    public Task<StatusActions> GetStatusActionsAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return Task.FromResult(new StatusActions(
            SeedIds.StatusPending,
            SeedIds.StatusInProgress,
            SeedIds.StatusHeld,
            SeedIds.StatusWorked,
            SeedIds.StatusCancelled,
            [SeedIds.StatusWorked, SeedIds.StatusQcd]));
    }

    private async Task<List<Guid>> NormalizeOrgIdsAsync(string role, IReadOnlyList<Guid> requested, CancellationToken cancellationToken)
    {
        var distinct = (requested ?? []).Distinct().ToList();
        if (Roles.IsGlobalAdmin(role))
        {
            return [];
        }

        if (distinct.Count == 0)
        {
            throw new ValidationException("Administrator, Editor, and Viewer accounts must be assigned to at least one organization.");
        }

        var existing = await _db.Organizations.CountAsync(x => distinct.Contains(x.Id), cancellationToken);
        if (existing != distinct.Count)
        {
            throw new ValidationException("One or more organizations were not found.");
        }

        if (!_currentUser.IsGlobalAdmin)
        {
            var allowed = await _orgScope.GetAllowedOrganizationIdsAsync(cancellationToken);
            if (distinct.Any(id => !allowed.Contains(id)))
            {
                throw new NotFoundException("Organization was not found.");
            }
        }

        return distinct;
    }

    private async Task<Organization> LoadScopedOrganizationAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureDirectoryManager();
        var org = await _db.Organizations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Organization was not found.");
        if (!_currentUser.IsGlobalAdmin)
        {
            var allowed = await _orgScope.GetAllowedOrganizationIdsAsync(cancellationToken);
            if (!allowed.Contains(org.Id))
            {
                throw new NotFoundException("Organization was not found.");
            }
        }

        return org;
    }

    private async Task<ApplicationUser> LoadScopedUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        EnsureDirectoryManager();
        var user = await _db.Users
            .Include(x => x.Organizations)
            .ThenInclude(x => x.Organization)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        var role = (await _users.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty;
        if (Roles.IsGlobalAdmin(role) && !_currentUser.IsGlobalAdmin)
        {
            throw new NotFoundException("User was not found.");
        }

        if (!_currentUser.IsGlobalAdmin)
        {
            var allowed = await _orgScope.GetAllowedOrganizationIdsAsync(cancellationToken);
            if (!user.Organizations.Any(x => allowed.Contains(x.OrganizationId)))
            {
                throw new NotFoundException("User was not found.");
            }
        }

        return user;
    }

    private void EnsureDirectoryManager()
    {
        if (!_currentUser.CanManageDirectory)
        {
            throw new ForbiddenException("Your role cannot manage users and organizations.");
        }
    }

    private static void ApplyParcelInventory(Organization org, int? total, int? withOwnership)
    {
        if (total is < 0 || withOwnership is < 0)
        {
            throw new ValidationException("Parcel counts cannot be negative.");
        }

        if (total is { } t && withOwnership is { } owned && owned > t)
        {
            throw new ValidationException("Parcels with ownership cannot exceed total real accounts.");
        }

        org.ParcelTotalRealAccounts = total;
        org.ParcelWithOwnership = withOwnership;
    }

    private async Task ReplaceAssignedTechsAsync(
        Organization org,
        IReadOnlyList<Guid> userIds,
        Guid? primaryAssignedTechId,
        CancellationToken cancellationToken)
    {
        var unique = userIds.Distinct().ToList();
        foreach (var userId in unique)
        {
            await EnsureAssignableAsync(userId, org.Id, cancellationToken);
        }

        var existing = await _db.OrganizationTechs.Where(x => x.OrganizationId == org.Id).ToListAsync(cancellationToken);
        var previousPrimary = existing.FirstOrDefault(x => x.IsPrimary)?.UserId;
        _db.OrganizationTechs.RemoveRange(existing);
        var primary = primaryAssignedTechId is { } requested && unique.Contains(requested)
            ? requested
            : previousPrimary is { } keep && unique.Contains(keep)
                ? keep
                : unique.FirstOrDefault();
        foreach (var userId in unique)
        {
            _db.OrganizationTechs.Add(new OrganizationTech
            {
                OrganizationId = org.Id,
                UserId = userId,
                IsPrimary = userId == primary
            });
        }
    }

    private async Task EnsureAssignableAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken)
            ?? throw new ValidationException("Assigned tech was not found.");
        var roles = await _users.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? string.Empty;
        if (!Roles.CanBeAssignedWork(role))
        {
            throw new ValidationException("Assigned techs must be Editors, Administrators, or a Global Administrator.");
        }

        if (!Roles.IsGlobalAdmin(role))
        {
            var linked = await _db.UserOrganizations.AnyAsync(
                x => x.UserId == userId && x.OrganizationId == organizationId,
                cancellationToken);
            if (!linked)
            {
                _db.UserOrganizations.Add(new UserOrganization { UserId = userId, OrganizationId = organizationId });
            }
        }
    }

    private async Task ReplaceUserOrganizationsAsync(
        Guid userId,
        string role,
        IReadOnlyList<Guid> orgIds,
        CancellationToken cancellationToken)
    {
        var existing = await _db.UserOrganizations.Where(x => x.UserId == userId).ToListAsync(cancellationToken);
        _db.UserOrganizations.RemoveRange(existing);
        foreach (var orgId in orgIds)
        {
            _db.UserOrganizations.Add(new UserOrganization { UserId = userId, OrganizationId = orgId });
        }

        await SyncAssignedTechsForUserAsync(userId, role, orgIds, cancellationToken);
        foreach (var orgId in orgIds.Concat(existing.Select(x => x.OrganizationId)).Distinct())
        {
            await EnsureOrgHasPrimaryAsync(orgId, cancellationToken);
        }
    }

    private async Task SyncAssignedTechsForUserAsync(
        Guid userId,
        string role,
        IReadOnlyList<Guid> orgIds,
        CancellationToken cancellationToken)
    {
        var techs = await _db.OrganizationTechs.Where(x => x.UserId == userId).ToListAsync(cancellationToken);
        if (Roles.IsGlobalAdmin(role))
        {
            return;
        }

        if (!Roles.CanBeAssignedWork(role))
        {
            _db.OrganizationTechs.RemoveRange(techs);
            return;
        }

        var keep = orgIds.ToHashSet();
        _db.OrganizationTechs.RemoveRange(techs.Where(x => !keep.Contains(x.OrganizationId)));
        var have = techs.Select(x => x.OrganizationId).ToHashSet();
        foreach (var orgId in keep)
        {
            if (have.Add(orgId))
            {
                var hasPrimary = await _db.OrganizationTechs.AnyAsync(
                    x => x.OrganizationId == orgId && x.IsPrimary,
                    cancellationToken);
                _db.OrganizationTechs.Add(new OrganizationTech
                {
                    OrganizationId = orgId,
                    UserId = userId,
                    IsPrimary = !hasPrimary
                });
            }
        }
    }

    private async Task EnsureOrgHasPrimaryAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var techs = _db.OrganizationTechs.Local
            .Where(x => x.OrganizationId == organizationId && _db.Entry(x).State != EntityState.Deleted)
            .ToList();
        if (techs.Count == 0)
        {
            techs = await _db.OrganizationTechs
                .Where(x => x.OrganizationId == organizationId)
                .ToListAsync(cancellationToken);
        }

        if (techs.Count == 0 || techs.Any(x => x.IsPrimary))
        {
            return;
        }

        techs.OrderBy(x => x.UserId).First().IsPrimary = true;
    }

    private Task<Organization> LoadOrganizationGraphAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Organizations
            .Include(x => x.AssignedTechs)
            .ThenInclude(x => x.User)
            .Include(x => x.Members)
            .ThenInclude(x => x.User)
            .FirstAsync(x => x.Id == id, cancellationToken);

    private static OrganizationDto MapOrg(Organization org) =>
        new(
            org.Id,
            org.Name,
            org.Code,
            org.IsActive,
            org.CreatedAt,
            !string.IsNullOrWhiteSpace(org.UploadToken),
            org.ParcelTotalRealAccounts,
            org.ParcelWithOwnership,
            org.TimeReportCardsVisible,
            Techs(org.AssignedTechs),
            People(org.Members?.Select(x => (x.UserId, x.User?.PublicName))));

    private async Task<string> UniqueUsernameAsync(
        Guid userId,
        string requested,
        string email,
        string? fullName,
        CancellationToken cancellationToken)
    {
        string seed;
        if (UserIdentity.LooksLikePersonName(requested))
        {
            seed = UserIdentity.FromPersonName(requested);
        }
        else if (UserIdentity.LooksLikePersonName(fullName))
        {
            seed = UserIdentity.FromPersonName(fullName!);
        }
        else
        {
            seed = UserIdentity.NormalizeUsername(requested);
        }

        if (string.IsNullOrEmpty(seed))
        {
            seed = UserIdentity.FromEmail(email);
        }

        var taken = await _db.Users.AsNoTracking()
            .Where(x => x.Id != userId && x.UserName != null)
            .Select(x => x.UserName!)
            .ToListAsync(cancellationToken);
        var set = taken
            .Select(UserIdentity.NormalizeUsername)
            .Where(x => x.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return UserIdentity.UniqueUsername(seed, set);
    }

    private static IReadOnlyList<AssignedTechDto> People(IEnumerable<(Guid Id, string? Name)>? rows) =>
        (rows ?? [])
            .Select(x => new AssignedTechDto(x.Id, x.Name ?? ""))
            .OrderBy(x => x.DisplayName)
            .ToList();

    private static IReadOnlyList<AssignedTechDto> Techs(IEnumerable<OrganizationTech>? rows) =>
        (rows ?? [])
            .Select(x => new AssignedTechDto(x.UserId, x.User?.PublicName ?? "", x.IsPrimary))
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.DisplayName)
            .ToList();

    private static UploadLinkDto MapLink(Organization org) =>
        new(org.Id, org.Name, org.UploadToken ?? string.Empty, $"/upload/{org.UploadToken}", org.UploadTokenCreatedAt);

    private static UserListItem MapUser(ApplicationUser user, string role) =>
        new(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            role,
            user.IsActive,
            user.Organizations.Select(x => new OrgMember(x.OrganizationId, x.Organization.Name)).ToList(),
            user.FullName,
            user.WorkPhone,
            !string.IsNullOrWhiteSpace(user.AvatarBlobPath));
}

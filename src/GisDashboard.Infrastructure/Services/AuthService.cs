using GisDashboard.Application.Abstractions;
using GisDashboard.Application.Auth;
using GisDashboard.Application.Exceptions;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;
using GisDashboard.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private static readonly HashSet<string> AvatarTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif"
    };

    private readonly UserManager<ApplicationUser> _users;
    private readonly AppDbContext _db;
    private readonly JwtTokenService _tokens;
    private readonly ICurrentUser _currentUser;
    private readonly IFileStorage _storage;

    public AuthService(
        UserManager<ApplicationUser> users,
        AppDbContext db,
        JwtTokenService tokens,
        ICurrentUser currentUser,
        IFileStorage storage)
    {
        _users = users;
        _db = db;
        _tokens = tokens;
        _currentUser = currentUser;
        _storage = storage;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var identifier = (request.UserName ?? request.Email ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ValidationException("Username or email and password are required.");
        }

        var user = await FindByUsernameOrEmailAsync(identifier);
        if (user is null || !UserIdentity.CanSignIn(user.IsActive, user.IsArchived))
        {
            throw new ForbiddenException("Invalid username or password.");
        }

        if (!await _users.CheckPasswordAsync(user, request.Password))
        {
            throw new ForbiddenException("Invalid username or password.");
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var role = await GetSingleRoleAsync(user);
        var (token, expires) = _tokens.Create(user, role);
        return new LoginResponse(token, expires, await MapUserAsync(user, role, cancellationToken));
    }

    public async Task<AuthUser> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new ForbiddenException("Authentication is required.");
        }

        var user = await _users.FindByIdAsync(_currentUser.UserId.ToString())
            ?? throw new NotFoundException("User was not found.");
        if (user.IsArchived)
        {
            throw new ForbiddenException("This account has been archived.");
        }

        var role = await GetSingleRoleAsync(user);
        return await MapUserAsync(user, role, cancellationToken);
    }

    public async Task<AuthUser> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await LoadSelfAsync();
        var userName = (request.UserName ?? request.DisplayName ?? string.Empty).Trim();
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new ValidationException("Username is required.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ValidationException("Email is required.");
        }

        if (await _users.FindByEmailAsync(email) is { } other && other.Id != user.Id)
        {
            throw new ConflictException("A user with that email already exists.");
        }

        var changingPassword = !string.IsNullOrWhiteSpace(request.NewPassword)
            || !string.IsNullOrWhiteSpace(request.ConfirmPassword);
        if (changingPassword)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            {
                throw new ValidationException("Current password is required to set a new password.");
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                throw new ValidationException("New password is required.");
            }

            if (request.NewPassword != request.ConfirmPassword)
            {
                throw new ValidationException("New password and confirmation do not match.");
            }

            if (request.NewPassword.Length < 8)
            {
                throw new ValidationException("New password must be at least 8 characters.");
            }

            if (!await _users.CheckPasswordAsync(user, request.CurrentPassword))
            {
                throw new ForbiddenException("Current password is not correct.");
            }

            var changed = await _users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!changed.Succeeded)
            {
                throw new ValidationException(string.Join(" ", changed.Errors.Select(e => e.Description)));
            }
        }

        var username = await UniqueUsernameAsync(user.Id, userName, email, cancellationToken);
        user.DisplayName = username;
        user.FullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim();
        user.WorkPhone = string.IsNullOrWhiteSpace(request.WorkPhone) ? null : request.WorkPhone.Trim();

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

        await _db.SaveChangesAsync(cancellationToken);
        var role = await GetSingleRoleAsync(user);
        return await MapUserAsync(user, role, cancellationToken);
    }

    public async Task<AuthUser> UploadAvatarAsync(
        Stream content,
        string fileName,
        string contentType,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        var user = await LoadSelfAsync();
        if (contentLength <= 0 || contentLength > 2 * 1024 * 1024)
        {
            throw new ValidationException("Photo must be an image of 2 MB or less.");
        }

        if (!AvatarTypes.Contains(contentType))
        {
            throw new ValidationException("Photo must be a JPEG, PNG, WebP, or GIF.");
        }

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext) || ext.Length > 8)
        {
            ext = contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ? ".png"
                : contentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase) ? ".webp"
                : contentType.Equals("image/gif", StringComparison.OrdinalIgnoreCase) ? ".gif"
                : ".jpg";
        }

        var path = $"avatars/{user.Id:N}/avatar{ext.ToLowerInvariant()}";
        user.AvatarBlobPath = await _storage.SaveRawAsync(path, content, contentType, cancellationToken);
        user.AvatarContentType = contentType;
        await _db.SaveChangesAsync(cancellationToken);
        var role = await GetSingleRoleAsync(user);
        return await MapUserAsync(user, role, cancellationToken);
    }

    public async Task<(Stream Stream, string ContentType)> GetAvatarAsync(CancellationToken cancellationToken = default)
    {
        var user = await LoadSelfAsync();
        if (string.IsNullOrWhiteSpace(user.AvatarBlobPath))
        {
            throw new NotFoundException("Photo was not found.");
        }

        var stream = await _storage.OpenReadAsync(user.AvatarBlobPath, cancellationToken);
        return (stream, string.IsNullOrWhiteSpace(user.AvatarContentType) ? "image/jpeg" : user.AvatarContentType);
    }

    private async Task<ApplicationUser> LoadSelfAsync()
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new ForbiddenException("Authentication is required.");
        }

        return await _users.FindByIdAsync(_currentUser.UserId.ToString())
            ?? throw new NotFoundException("User was not found.");
    }

    private async Task<AuthUser> MapUserAsync(ApplicationUser user, string role, CancellationToken cancellationToken)
    {
        List<OrgOption> orgs;
        if (Roles.CanSeeAllOrganizations(role))
        {
            orgs = await _db.Organizations.AsNoTracking()
                .Where(x => x.IsActive && !x.IsArchived)
                .OrderBy(x => x.Name)
                .Select(x => new OrgOption(x.Id, x.Name, x.Code, x.IsArchived))
                .ToListAsync(cancellationToken);
        }
        else
        {
            orgs = await _db.UserOrganizations.AsNoTracking()
                .Where(x => x.UserId == user.Id && !x.Organization.IsArchived)
                .OrderBy(x => x.Organization.Name)
                .Select(x => new OrgOption(x.Organization.Id, x.Organization.Name, x.Organization.Code, x.Organization.IsArchived))
                .ToListAsync(cancellationToken);
        }

        var clientCardsVisible = Roles.IsGlobalAdmin(role)
            || await _db.UserOrganizations.AsNoTracking()
                .AnyAsync(x => x.UserId == user.Id && x.Organization.TimeReportCardsVisible, cancellationToken);
        var canViewTimeReport = Roles.IsGlobalAdmin(role)
            || role is Roles.Editor or Roles.Administrator
            || clientCardsVisible;
        var canViewTeamTimeReport = Roles.IsGlobalAdmin(role)
            || ((role is Roles.Administrator || Roles.IsOrgScopedClient(role)) && clientCardsVisible);

        return new AuthUser(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            role,
            Roles.DisplayName(role),
            orgs,
            Roles.CanSeeInternalNotes(role),
            Roles.CanEditInternalNotes(role),
            Roles.CanSeeTimeLogs(role),
            Roles.CanLogTime(role),
            Roles.CanUpload(role),
            Roles.CanMutateWorkItems(role),
            Roles.CanManageDirectory(role),
            Roles.CanManageGlobalDirectory(role),
            Roles.CanManageAssignedTechs(role),
            canViewTimeReport,
            canViewTeamTimeReport,
            Roles.CanSeeAllOrganizations(role),
            user.FullName,
            user.WorkPhone,
            !string.IsNullOrWhiteSpace(user.AvatarBlobPath),
            user.UserName,
            Roles.CanSeePresence(role),
            Roles.CanSeeConnections(role),
            Roles.CanManageConnections(role),
            Roles.CanPostComments(role),
            Roles.CanSeeDashboardAssignee(role));
    }

    private async Task<ApplicationUser?> FindByUsernameOrEmailAsync(string identifier)
    {
        if (UserIdentity.LooksLikeEmail(identifier))
        {
            return await _users.FindByEmailAsync(identifier)
                   ?? await _users.FindByNameAsync(identifier);
        }

        return await _users.FindByNameAsync(identifier)
               ?? await _users.FindByEmailAsync(identifier);
    }

    private async Task<string> UniqueUsernameAsync(
        Guid userId,
        string requested,
        string email,
        CancellationToken cancellationToken)
    {
        var seed = UserIdentity.NormalizeUsername(requested);
        if (string.IsNullOrEmpty(seed))
        {
            seed = UserIdentity.FromEmail(email);
        }

        if (string.IsNullOrEmpty(seed))
        {
            throw new ValidationException("Username is required.");
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

    private async Task<string> GetSingleRoleAsync(ApplicationUser user)
    {
        var roles = await _users.GetRolesAsync(user);
        return roles.FirstOrDefault()
            ?? throw new ConflictException("User is missing a role assignment.");
    }
}

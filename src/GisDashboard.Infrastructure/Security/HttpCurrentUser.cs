using System.Security.Claims;
using GisDashboard.Application.Abstractions;
using GisDashboard.Domain;
using Microsoft.AspNetCore.Http;

namespace GisDashboard.Infrastructure.Security;

public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;

    public HttpCurrentUser(IHttpContextAccessor http)
    {
        _http = http;
    }

    private ClaimsPrincipal? Principal => _http.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid UserId
    {
        get
        {
            var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    public string Email => Principal?.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

    public string DisplayName => Principal?.FindFirstValue(ClaimTypes.Name) ?? Email;

    public string Role => Principal?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    public bool IsGlobalAdmin => Roles.IsGlobalAdmin(Role);
    public bool IsOrgAdmin => Roles.IsOrgAdmin(Role);
    public bool IsAdmin => Roles.CanManageTimeBroadly(Role);
    public bool CanSeeInternalNotes => Roles.CanSeeInternalNotes(Role);
    public bool CanPostComments => Roles.CanPostComments(Role);
    public bool CanEditInternalNotes => Roles.CanEditInternalNotes(Role);
    public bool CanSeeTimeLogs => Roles.CanSeeTimeLogs(Role);
    public bool CanLogTime => Roles.CanLogTime(Role);
    public bool CanSeeAllOrganizations => Roles.CanSeeAllOrganizations(Role);
    public bool CanUpload => Roles.CanUpload(Role);
    public bool CanMutateWorkItems => Roles.CanMutateWorkItems(Role);
    public bool CanManageDirectory => Roles.CanManageDirectory(Role);
    public bool CanManageGlobalDirectory => Roles.CanManageGlobalDirectory(Role);
    public bool CanSeePresence => Roles.CanSeePresence(Role);
    public bool CanSeeConnections => Roles.CanSeeConnections(Role);
    public bool CanManageConnections => Roles.CanManageConnections(Role);
}

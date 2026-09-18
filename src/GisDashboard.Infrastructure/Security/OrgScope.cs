using GisDashboard.Application.Abstractions;
using GisDashboard.Application.Exceptions;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Security;

public sealed class OrgScope : IOrgScope
{
    private static readonly Dictionary<Guid, string> SeedOrgCodes = new()
    {
        [SeedIds.DemoClient] = "DEMOCLIENT",
        [SeedIds.OtherClient] = "OTHERCLIENT"
    };

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public OrgScope(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyCollection<Guid>> GetAllowedOrganizationIdsAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new ForbiddenException("Authentication is required.");
        }

        if (_currentUser.CanSeeAllOrganizations)
        {
            return await _db.Organizations.AsNoTracking()
                .Where(x => x.IsActive)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
        }

        return await _db.UserOrganizations.AsNoTracking()
            .Where(x => x.UserId == _currentUser.UserId && x.Organization.IsActive)
            .Select(x => x.OrganizationId)
            .ToListAsync(cancellationToken);
    }

    public async Task EnsureCanAccessOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ValidationException("An organization is required.");
        }

        var org = await _db.Organizations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == organizationId, cancellationToken);
        if (org is null)
        {
            throw new NotFoundException("Organization was not found.");
        }

        if (_currentUser.CanSeeAllOrganizations)
        {
            return;
        }

        var linked = await _db.UserOrganizations.AsNoTracking()
            .AnyAsync(x => x.UserId == _currentUser.UserId && x.OrganizationId == organizationId, cancellationToken);
        if (!linked)
        {
            throw new NotFoundException("Organization was not found.");
        }
    }

    public async Task<Guid> ResolveAccessibleOrganizationAsync(
        Guid? organizationId,
        string? nameOrCode,
        CancellationToken cancellationToken = default)
    {
        Organization? org = null;
        if (organizationId is { } id && id != Guid.Empty)
        {
            org = await _db.Organizations.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (org is null && SeedOrgCodes.TryGetValue(id, out var seedCode))
            {
                org = await FindByNameOrCodeAsync(seedCode, cancellationToken);
            }
        }

        if (org is null && !string.IsNullOrWhiteSpace(nameOrCode))
        {
            org = await FindByNameOrCodeAsync(nameOrCode, cancellationToken);
        }

        if (org is null)
        {
            if ((organizationId is null || organizationId == Guid.Empty) && string.IsNullOrWhiteSpace(nameOrCode))
            {
                // WL04 — single-org clients (Uploader) auto-resolve their assigned organization.
                // Staff (all-orgs) still must pick a client.
                if (!_currentUser.CanSeeAllOrganizations)
                {
                    var allowed = await GetAllowedOrganizationIdsAsync(cancellationToken);
                    if (allowed.Count == 1)
                    {
                        return allowed.First();
                    }
                }

                throw new ValidationException("An organization is required.");
            }

            throw new NotFoundException("Organization was not found.");
        }

        await EnsureCanAccessOrganizationAsync(org.Id, cancellationToken);
        return org.Id;
    }

    private async Task<Organization?> FindByNameOrCodeAsync(string nameOrCode, CancellationToken cancellationToken)
    {
        var key = nameOrCode.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return await _db.Organizations.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Name.ToLower() == key || x.Code.ToLower() == key,
                cancellationToken);
    }
}

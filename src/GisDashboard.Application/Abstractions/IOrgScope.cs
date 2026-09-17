namespace GisDashboard.Application.Abstractions;

public interface IOrgScope
{
    Task<IReadOnlyCollection<Guid>> GetAllowedOrganizationIdsAsync(CancellationToken cancellationToken = default);
    Task EnsureCanAccessOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<Guid> ResolveAccessibleOrganizationAsync(Guid? organizationId, string? nameOrCode, CancellationToken cancellationToken = default);
}

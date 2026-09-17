namespace GisDashboard.Application.Status;

public interface IEnvironmentStatusService
{
    Task<EnvironmentStatusResponse> GetAsync(CancellationToken cancellationToken = default);
}

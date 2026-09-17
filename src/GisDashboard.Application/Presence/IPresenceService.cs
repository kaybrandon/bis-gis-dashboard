namespace GisDashboard.Application.Presence;

public interface IPresenceService
{
    Task HeartbeatAsync(PresenceHeartbeatRequest request, CancellationToken cancellationToken = default);
    Task<PresenceListResponse> ListAsync(CancellationToken cancellationToken = default);
    Task<(Stream Stream, string ContentType)> GetAvatarAsync(Guid userId, CancellationToken cancellationToken = default);
}

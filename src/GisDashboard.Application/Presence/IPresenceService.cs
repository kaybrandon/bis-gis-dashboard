namespace GisDashboard.Application.Presence;

public interface IPresenceService
{
    Task HeartbeatAsync(PresenceHeartbeatRequest request, CancellationToken cancellationToken = default);
    Task<PresenceListResponse> ListAsync(CancellationToken cancellationToken = default);
    Task<NeedHelpResponse> SetNeedsHelpAsync(bool needsHelp, CancellationToken cancellationToken = default);
    Task<(Stream Stream, string ContentType)> GetAvatarAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> CanSeePresenceUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<string?> PresenceStatusOfAsync(Guid userId, CancellationToken cancellationToken = default);
}

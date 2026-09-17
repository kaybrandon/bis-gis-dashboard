namespace GisDashboard.Application.Presence;

public sealed record PresenceHeartbeatRequest(
    string? Route,
    Guid? WorkItemId,
    bool ClockedIn,
    Guid? ClockWorkItemId);

public sealed record PresenceUserDto(
    Guid UserId,
    string DisplayName,
    bool HasAvatar,
    string PresenceStatus,
    string PageName,
    Guid? WorkItemId,
    string? WorkItemTitle,
    bool ClockedIn,
    DateTimeOffset LastSeen);

public sealed record PresenceListResponse(
    IReadOnlyList<PresenceUserDto> Items,
    int OnlineCount);

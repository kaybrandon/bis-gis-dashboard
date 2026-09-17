namespace GisDashboard.Application.Status;

public sealed record EnvironmentStatusResponse(
    string Overall,
    DateTimeOffset CheckedAt,
    string Note,
    IReadOnlyList<EnvironmentCheck> Checks);

public sealed record EnvironmentCheck(
    string Key,
    string Name,
    string Status,
    string Mode,
    string Detail,
    string? Error);

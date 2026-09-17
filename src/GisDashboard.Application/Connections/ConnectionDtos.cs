namespace GisDashboard.Application.Connections;

public sealed record FileServerDto(Guid Id, string Name, string RootPath, string SourceRoot);

public sealed record FileConnectionDto(
    Guid Id,
    Guid OrganizationId,
    string OrganizationName,
    Guid? FileServerId,
    string? SourceRoot,
    string? FileServerRoot,
    string SourcePath,
    string? FtpFolder,
    string? FtpUrl,
    string? FtpUserName,
    bool PasswordConfigured,
    bool Enabled,
    string? Status,
    string? LastError,
    DateTimeOffset? LastPublishedAt,
    int LastFileCount,
    string? LastZipName,
    bool HasPackage,
    bool CanDownload,
    bool CanManage);

public sealed record AgentTelemetryDto(
    string? HostName,
    string? OsDescription,
    string? OsVersion,
    string? Arch,
    string? RuntimeVersion,
    string? AgentVersion,
    long? FreeDiskBytes,
    string? LastError,
    string? LocalIp,
    string? PublicIp);

public sealed record LanConnectionDto(
    Guid Id,
    Guid OrganizationId,
    string OrganizationName,
    string RemoteFolder,
    string BisFolder,
    string Direction,
    int ScheduleMinutes,
    bool Enrolled,
    string? EnrollToken,
    string? EnrollTokenMasked,
    string Status,
    bool HeartbeatOk,
    DateTimeOffset? LastHeartbeatAt,
    DateTimeOffset? LastSyncAt,
    int LastPullCount,
    int LastPushCount,
    string? LastError,
    string? LastErrorCode,
    DateTimeOffset? LastErrorAt,
    string? MachineName,
    string? LocalIp,
    string? PublicIp,
    string? AgentVersion,
    AgentTelemetryDto? Telemetry,
    bool RestartPending,
    bool RestartAvailable,
    string? RestartResult,
    bool UpdatePending,
    bool UpdateAvailable,
    string? UpdateTargetVersion,
    string? UpdateStatus,
    string? PublishedAgentVersion,
    bool RunNowQueued,
    bool CanManage);

public sealed record UpsertFileConnectionRequest(
    Guid? OrganizationId,
    Guid? FileServerId,
    string? SourcePath,
    string? FtpFolder,
    string? FtpUrl,
    string? FtpUserName,
    string? FtpPassword,
    bool? Enabled);

public sealed record UpsertLanConnectionRequest(
    Guid? OrganizationId,
    string? RemoteFolder,
    string? BisFolder,
    string? Direction,
    int? ScheduleMinutes);

public sealed record FolderCheckResult(bool Ok, string Result, string Message, string Kind);

public sealed record CheckFoldersResponse(
    FolderCheckResult Source,
    FolderCheckResult Destination,
    bool BothPassed,
    bool CanRunNow);

public sealed record CheckFoldersRequest(string? SourcePath, string? RemoteFolder, Guid? FileServerId);

public sealed record AgentHeartbeatRequest(
    string? EnrollToken,
    string? MachineName,
    string? LocalIp,
    string? PublicIp,
    string? AgentVersion,
    string? HostName,
    string? OsDescription,
    string? OsVersion,
    string? Arch,
    string? RuntimeVersion,
    long? FreeDiskBytes,
    bool? SourceExists,
    bool? DestinationExists,
    int? LocalFileCount,
    string? LastError,
    string? LastErrorCode,
    int? LastPullCount,
    int? LastPushCount);

public sealed record SyncControlDto(bool Paused, string? Message);

public sealed record FileKindDto(string Key, string DisplayName, string Extensions, bool Enabled);

public sealed record FileKindsResponse(IReadOnlyList<FileKindDto> Kinds);

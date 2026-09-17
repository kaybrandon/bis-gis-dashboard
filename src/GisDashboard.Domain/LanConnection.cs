namespace GisDashboard.Domain;

/// <summary>Windows SyncAgent row returned by GET /api/lan-connections.</summary>
public sealed class LanConnection
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    /// <summary>Destination — PC/share or Azure /orgs/ prefix.</summary>
    public string RemoteFolder { get; set; } = string.Empty;

    /// <summary>Source — PC/share or Azure /orgs/ prefix.</summary>
    public string BisFolder { get; set; } = string.Empty;

    public string Direction { get; set; } = "Bidirectional";
    public int ScheduleMinutes { get; set; } = 15;

    public string EnrollTokenHash { get; set; } = string.Empty;
    public string EnrollTokenMasked { get; set; } = string.Empty;
    public bool Enrolled { get; set; }

    public string Status { get; set; } = "Idle";
    public bool HeartbeatOk { get; set; }
    public DateTimeOffset? LastHeartbeatAt { get; set; }
    public DateTimeOffset? LastSyncAt { get; set; }
    public int LastPullCount { get; set; }
    public int LastPushCount { get; set; }

    public string? LastError { get; set; }
    public string? LastErrorCode { get; set; }
    public DateTimeOffset? LastErrorAt { get; set; }

    public string? MachineName { get; set; }
    public string? LocalIp { get; set; }
    public string? PublicIp { get; set; }
    public string? AgentVersion { get; set; }
    public string? HostName { get; set; }
    public string? OsDescription { get; set; }
    public string? OsVersion { get; set; }
    public string? Arch { get; set; }
    public string? RuntimeVersion { get; set; }
    public long? FreeDiskBytes { get; set; }

    public bool SourceExistsOnAgent { get; set; }
    public bool DestinationExistsOnAgent { get; set; }
    public int AgentLocalFileCount { get; set; }
    public bool RunNowQueued { get; set; }

    public bool RestartPending { get; set; }
    public bool UpdatePending { get; set; }
    public string? RestartResult { get; set; }
    public string? UpdateStatus { get; set; }
    public string? UpdateTargetVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

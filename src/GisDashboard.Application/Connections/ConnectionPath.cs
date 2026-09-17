namespace GisDashboard.Application.Connections;

/// <summary>
/// Connection Source / Destination paths. Azure folders are the
/// <c>workfiles</c> container plus an <c>orgs/…</c> prefix — displayed
/// and persisted as <c>workfiles/orgs/…</c>, never a PC or file-server
/// folder. Bare <c>/orgs/…</c> is accepted as the same Azure folder.
/// </summary>
public static class ConnectionPath
{
    public const string AzureContainerName = "workfiles";
    public const string AzureAccountName = "stbisgisdashboard";
    public const string AzureKeepBlobName = ".keep";
    public const string AzureResourceGroup = "rg-bis-gis-dashboard";

    /// <summary>
    /// Agent schedule defaults to 15 minutes; keep the card Online across one missed beat.
    /// </summary>
    public static readonly TimeSpan HeartbeatFreshWindow = TimeSpan.FromMinutes(30);

    public static bool IsAzureOrgPath(string? path)
    {
        var normalized = StripContainer(NormalizeSlashes(path));
        return normalized.StartsWith("orgs/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Blob prefix without container or leading slash, e.g. <c>orgs/democlient/shapefiles</c>.
    /// </summary>
    public static string ToAzurePrefix(string? path)
    {
        var normalized = StripContainer(NormalizeSlashes(path)).TrimEnd('/');
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Azure folder path is required.", nameof(path));
        }

        if (!normalized.StartsWith("orgs/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("This path is not an Azure workfiles/orgs/ folder.", nameof(path));
        }

        return normalized;
    }

    /// <summary>
    /// Canonical Azure display and persist form: <c>workfiles/orgs/…</c>.
    /// Local / UNC paths are trimmed only.
    /// </summary>
    public static string DisplayPath(string? path)
    {
        if (!IsAzureOrgPath(path))
        {
            return (path ?? string.Empty).Trim();
        }

        return AzureContainerName + "/" + ToAzurePrefix(path);
    }

    public static string Canonicalize(string? path)
    {
        var trimmed = (path ?? string.Empty).Trim();
        return IsAzureOrgPath(trimmed) ? DisplayPath(trimmed) : trimmed;
    }

    public static string KindLabel(string? path) => InferSourceKind(path);

    public static string InferSourceKind(string? path)
    {
        if (IsAzureOrgPath(path))
        {
            return "azure";
        }

        if (IsUncPath(path))
        {
            return "unc";
        }

        return "local";
    }

    public static bool IsUncPath(string? path)
    {
        var trimmed = (path ?? string.Empty).Trim();
        return trimmed.StartsWith(@"\\", StringComparison.Ordinal)
            || trimmed.StartsWith("//", StringComparison.Ordinal);
    }

    public static bool IsLocalDrivePath(string? path)
    {
        var trimmed = (path ?? string.Empty).Trim();
        return trimmed.Length >= 3
            && char.IsLetter(trimmed[0])
            && trimmed[1] == ':'
            && (trimmed[2] == '\\' || trimmed[2] == '/');
    }

    public static string NormalizeSlashes(string? path)
    {
        var value = (path ?? string.Empty).Trim().Replace('\\', '/');
        while (value.StartsWith('/'))
        {
            value = value[1..];
        }

        return value;
    }

    public static string CheckerIdentity()
    {
        var user = Environment.UserName;
        var domain = Environment.UserDomainName;
        var machine = Environment.MachineName;
        if (string.IsNullOrWhiteSpace(user))
        {
            user = "unknown-user";
        }

        if (string.IsNullOrWhiteSpace(machine))
        {
            machine = "unknown-pc";
        }

        if (!string.IsNullOrWhiteSpace(domain)
            && !string.Equals(domain, machine, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(domain, ".", StringComparison.Ordinal))
        {
            return $@"{domain}\{user} on {machine}";
        }

        return $"{user} on {machine}";
    }

    public static string AgentIdentity(string? windowsUserName, string? machineName, string? hostName)
    {
        var pc = FirstNonEmpty(machineName, hostName) ?? "unknown-pc";
        var user = string.IsNullOrWhiteSpace(windowsUserName) ? "unknown-user" : windowsUserName.Trim();
        return $"{user} on {pc}";
    }

    public static bool HeartbeatIsFresh(DateTimeOffset? lastHeartbeatAt, DateTimeOffset? now = null)
    {
        if (lastHeartbeatAt is null)
        {
            return false;
        }

        var clock = now ?? DateTimeOffset.UtcNow;
        return clock - lastHeartbeatAt.Value <= HeartbeatFreshWindow;
    }

    public static string HeartbeatLabel(DateTimeOffset? lastHeartbeatAt, bool enrolled)
    {
        if (lastHeartbeatAt is DateTimeOffset at)
        {
            return at.ToString("u");
        }

        return enrolled
            ? "Never — enrolled agent has not checked in"
            : "None — agent not enrolled or not running";
    }

    private static string StripContainer(string normalized)
    {
        if (normalized.StartsWith(AzureContainerName + "/", StringComparison.OrdinalIgnoreCase))
        {
            return normalized[(AzureContainerName.Length + 1)..];
        }

        return normalized;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}

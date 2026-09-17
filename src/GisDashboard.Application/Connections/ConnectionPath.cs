namespace GisDashboard.Application.Connections;

/// <summary>
/// Connection Source / Destination paths. Paths that start with
/// <c>/orgs/</c> or <c>orgs/</c> are BIS Azure blob prefixes under
/// container <c>workfiles</c> — never PC or file-server folders.
/// </summary>
public static class ConnectionPath
{
    public const string AzureContainerName = "workfiles";
    public const string AzureAccountName = "stbisgisdashboard";
    public const string AzureKeepBlobName = ".keep";

    public static bool IsAzureOrgPath(string? path)
    {
        var normalized = NormalizeSlashes(path);
        return normalized.StartsWith("orgs/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Blob prefix without a leading slash, e.g. <c>orgs/democlient/shapefiles</c>.
    /// </summary>
    public static string ToAzurePrefix(string? path)
    {
        var normalized = NormalizeSlashes(path).TrimEnd('/');
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Azure folder path is required.", nameof(path));
        }

        if (!normalized.StartsWith("orgs/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("This path is not an Azure /orgs/ folder.", nameof(path));
        }

        return normalized;
    }

    public static string DisplayPath(string? path)
    {
        if (!IsAzureOrgPath(path))
        {
            return (path ?? string.Empty).Trim();
        }

        return "/" + ToAzurePrefix(path);
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
}

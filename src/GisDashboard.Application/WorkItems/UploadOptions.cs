namespace GisDashboard.Application.WorkItems;

/// <summary>
/// Per-file upload limits. Default is 50 MB per file. Change in Azure with
/// <c>Uploads__MaxFileMegabytes</c> (25–50 typical) or <c>Uploads__MaxFileBytes</c>.
/// Browser queues run <see cref="Concurrency"/> uploads at a time.
/// </summary>
public sealed class UploadOptions
{
    public const string SectionName = "Uploads";

    /// <summary>50 MiB. The documented default per-file limit.</summary>
    public const long DefaultMaxFileBytes = 52_428_800;

    public const int DefaultConcurrency = 3;

    /// <summary>
    /// HTTP / IIS request ceiling (100 MiB) so a 50 MB PDF plus multipart overhead is accepted.
    /// Keep <see cref="EffectiveMaxFileBytes"/> below this.
    /// </summary>
    public const long HttpRequestCeilingBytes = 104_857_600;

    public long MaxFileBytes { get; set; } = DefaultMaxFileBytes;

    /// <summary>When set and greater than zero, overrides <see cref="MaxFileBytes"/> (1 = 1 MiB).</summary>
    public int? MaxFileMegabytes { get; set; }

    public int Concurrency { get; set; } = DefaultConcurrency;

    public long EffectiveMaxFileBytes
    {
        get
        {
            if (MaxFileMegabytes is > 0)
            {
                return MaxFileMegabytes.Value * 1024L * 1024L;
            }

            return MaxFileBytes > 0 ? MaxFileBytes : DefaultMaxFileBytes;
        }
    }

    public int EffectiveConcurrency => Concurrency is >= 1 and <= 8 ? Concurrency : DefaultConcurrency;

    public int EffectiveMaxFileMegabytes
    {
        get
        {
            var bytes = EffectiveMaxFileBytes;
            var rounded = (int)Math.Round(bytes / (1024d * 1024d), MidpointRounding.AwayFromZero);
            return Math.Max(1, rounded);
        }
    }

    public static string OversizedMessage(string? fileName, long sizeBytes, long maxBytes)
    {
        var name = string.IsNullOrWhiteSpace(fileName) ? "This file" : Path.GetFileName(fileName);
        return $"{name} is {FormatSize(sizeBytes)}, which is over the {FormatSize(maxBytes)} per-file limit. That file was not uploaded.";
    }

    public static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} bytes";
        }

        if (bytes < 1024L * 1024L)
        {
            return $"{bytes / 1024d:0.#} KB";
        }

        return $"{bytes / (1024d * 1024d):0.#} MB";
    }
}

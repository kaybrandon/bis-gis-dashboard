using GisDashboard.Application.Abstractions;
using GisDashboard.Application.Connections;
using Microsoft.Extensions.Options;

namespace GisDashboard.Infrastructure.Storage;

public sealed class LocalStorageOptions
{
    public string RootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "workfiles");
}

public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<LocalStorageOptions> options)
    {
        _root = options.Value.RootPath;
        Directory.CreateDirectory(_root);
    }

    public string ProviderLabel => "Local disk";
    public string ContainerName => "workfiles";

    public async Task<string> SaveAsync(
        Guid organizationId,
        Guid workItemId,
        string fileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = $"{workItemId:N}.bin";
        }

        var relative = Path.Combine(organizationId.ToString("N"), workItemId.ToString("N"), safeName);
        var full = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        await using var output = File.Create(full);
        await content.CopyToAsync(output, cancellationToken);
        return relative.Replace('\\', '/');
    }

    public async Task<string> SaveRawAsync(
        string relativePath,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var safe = relativePath.Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(safe) || safe.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Avatar path is not valid.");
        }

        var full = Path.Combine(_root, safe.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var output = File.Create(full);
        await content.CopyToAsync(output, cancellationToken);
        return safe;
    }

    public Task<Stream> OpenReadAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var full = Path.Combine(_root, blobPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(full))
        {
            throw new FileNotFoundException("Work file was not found in local storage.", full);
        }

        Stream stream = File.OpenRead(full);
        return Task.FromResult(stream);
    }

    public Task<StorageProbe> ProbeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_root);
        var probe = Path.Combine(_root, ".health");
        File.WriteAllText(probe, DateTimeOffset.UtcNow.ToString("O"));
        return Task.FromResult(new StorageProbe(true, "Local disk", "Local workfiles folder is writable.", null));
    }

    public Task<StoragePrefixProbe> ProbePrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var folder = PrefixFolder(prefix);
        try
        {
            var files = Directory.Exists(folder)
                ? Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Count(path => !IsPlaceholder(path))
                : 0;
            return Task.FromResult(new StoragePrefixProbe(
                true,
                true,
                files,
                null,
                null,
                $"Azure folder {ConnectionPath.DisplayPath(prefix)} is reachable ({files} file{(files == 1 ? "" : "s")})."));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(new StoragePrefixProbe(
                false,
                false,
                0,
                SyncErrorCodes.AzurePrefixForbidden,
                $"Azure folder {ConnectionPath.DisplayPath(prefix)} could not be listed. Check storage permissions.",
                ex.Message));
        }
    }

    public Task EnsurePrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var folder = PrefixFolder(prefix);
        Directory.CreateDirectory(folder);
        var keep = Path.Combine(folder, ConnectionPath.AzureKeepBlobName);
        if (!File.Exists(keep) && !Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Any())
        {
            File.WriteAllBytes(keep, Array.Empty<byte>());
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StoredObjectInfo>> ListPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var folder = PrefixFolder(prefix);
        if (!Directory.Exists(folder))
        {
            return Task.FromResult<IReadOnlyList<StoredObjectInfo>>([]);
        }

        var items = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Select(path =>
            {
                var relative = Path.GetRelativePath(folder, path).Replace('\\', '/');
                var info = new FileInfo(path);
                return new StoredObjectInfo(
                    $"{NormalizePrefix(prefix)}/{relative}",
                    relative,
                    info.Length,
                    info.LastWriteTimeUtc,
                    IsPlaceholder(path));
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<StoredObjectInfo>>(items);
    }

    public async Task<string> SaveUnderPrefixAsync(
        string prefix,
        string relativeName,
        Stream content,
        string contentType,
        DateTimeOffset? lastWriteUtc = null,
        CancellationToken cancellationToken = default)
    {
        var folder = PrefixFolder(prefix);
        var safeName = relativeName.Replace('\\', '/').TrimStart('/');
        if (safeName.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("File path is not valid.");
        }

        var full = Path.Combine(folder, safeName.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using (var output = File.Create(full))
        {
            await content.CopyToAsync(output, cancellationToken);
        }

        if (lastWriteUtc.HasValue)
        {
            File.SetLastWriteTimeUtc(full, lastWriteUtc.Value.UtcDateTime);
        }

        return $"{NormalizePrefix(prefix)}/{safeName}";
    }

    private string PrefixFolder(string prefix) =>
        Path.Combine(_root, NormalizePrefix(prefix).Replace('/', Path.DirectorySeparatorChar));

    private static string NormalizePrefix(string prefix) =>
        prefix.Replace('\\', '/').Trim().Trim('/');

    private static bool IsPlaceholder(string path) =>
        string.Equals(Path.GetFileName(path), ConnectionPath.AzureKeepBlobName, StringComparison.OrdinalIgnoreCase);
}

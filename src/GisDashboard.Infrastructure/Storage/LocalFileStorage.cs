using GisDashboard.Application.Abstractions;
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
}

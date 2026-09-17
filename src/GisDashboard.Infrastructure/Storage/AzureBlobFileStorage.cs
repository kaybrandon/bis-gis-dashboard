using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using GisDashboard.Application.Abstractions;
using GisDashboard.Application.Connections;
using Microsoft.Extensions.Options;

namespace GisDashboard.Infrastructure.Storage;

public sealed class AzureStorageOptions
{
    public string? ConnectionString { get; set; }
    public string AccountName { get; set; } = "stbisgisdashboard";
    public string ContainerName { get; set; } = "workfiles";
}

public sealed class AzureBlobFileStorage : IFileStorage
{
    private readonly BlobContainerClient _container;

    public AzureBlobFileStorage(IOptions<AzureStorageOptions> options)
    {
        var settings = options.Value;
        if (!string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            _container = new BlobContainerClient(settings.ConnectionString, settings.ContainerName);
        }
        else
        {
            var uri = new Uri($"https://{settings.AccountName}.blob.core.windows.net/{settings.ContainerName}");
            _container = new BlobContainerClient(uri, new DefaultAzureCredential());
        }
    }

    public string ProviderLabel => "Azure Blob";
    public string ContainerName => _container.Name;

    public async Task<string> SaveAsync(
        Guid organizationId,
        Guid workItemId,
        string fileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        var safeName = Path.GetFileName(fileName);
        var blobName = $"{organizationId:N}/{workItemId:N}/{safeName}";
        var blob = _container.GetBlobClient(blobName);
        await blob.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: cancellationToken);
        return blobName;
    }

    public async Task<string> SaveRawAsync(
        string relativePath,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        var safe = relativePath.Replace('\\', '/').TrimStart('/');
        var blob = _container.GetBlobClient(safe);
        await blob.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: cancellationToken);
        return safe;
    }

    public async Task<Stream> OpenReadAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var blob = _container.GetBlobClient(blobPath);
        var response = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
    }

    public async Task<StorageProbe> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var container = _container.Name;
        var account = _container.AccountName;
        var exists = await _container.ExistsAsync(cancellationToken);
        if (!exists.Value)
        {
            return new StorageProbe(false, "Azure Blob", $"Container {container} was not found.", "Container does not exist or this identity cannot see it.");
        }

        await _container.GetPropertiesAsync(cancellationToken: cancellationToken);
        var where = string.IsNullOrWhiteSpace(account) ? container : $"{account}/{container}";
        return new StorageProbe(true, "Azure Blob", $"Container {where} is reachable.", null);
    }

    public async Task<StoragePrefixProbe> ProbePrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var safe = prefix.Replace('\\', '/').Trim().Trim('/');
        try
        {
            var exists = await _container.ExistsAsync(cancellationToken);
            if (!exists.Value)
            {
                return new StoragePrefixProbe(
                    false,
                    false,
                    0,
                    SyncErrorCodes.AzurePrefixNotFound,
                    $"Azure folder {ConnectionPath.DisplayPath(safe)} was not found.",
                    $"Container {_container.Name} was not found or this identity cannot see it.");
            }

            var files = await ListPrefixAsync(safe, cancellationToken);
            var real = files.Count(x => !x.IsPlaceholder);
            return new StoragePrefixProbe(
                true,
                true,
                real,
                null,
                null,
                $"Azure folder {ConnectionPath.DisplayPath(safe)} is reachable ({real} file{(real == 1 ? "" : "s")}).");
        }
        catch (RequestFailedException ex) when (ex.Status is 401 or 403)
        {
            return new StoragePrefixProbe(
                false,
                false,
                0,
                SyncErrorCodes.AzurePrefixForbidden,
                $"Azure folder {ConnectionPath.DisplayPath(safe)} could not be listed. Check storage permissions.",
                ex.Message);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new StoragePrefixProbe(
                false,
                false,
                0,
                SyncErrorCodes.AzurePrefixNotFound,
                $"Azure folder {ConnectionPath.DisplayPath(safe)} was not found.",
                ex.Message);
        }
        catch (RequestFailedException ex)
        {
            return new StoragePrefixProbe(
                false,
                false,
                0,
                SyncErrorCodes.AzurePrefixForbidden,
                $"Azure folder {ConnectionPath.DisplayPath(safe)} could not be listed. Check storage permissions.",
                ex.Message);
        }
    }

    public async Task EnsurePrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        var files = await ListPrefixAsync(prefix, cancellationToken);
        if (files.Count > 0)
        {
            return;
        }

        var safe = prefix.Replace('\\', '/').Trim().Trim('/');
        var blob = _container.GetBlobClient($"{safe}/{ConnectionPath.AzureKeepBlobName}");
        await using var empty = new MemoryStream(Array.Empty<byte>());
        await blob.UploadAsync(empty, overwrite: true, cancellationToken);
    }

    public async Task<IReadOnlyList<StoredObjectInfo>> ListPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var safe = prefix.Replace('\\', '/').Trim().Trim('/');
        var withSlash = safe + "/";
        var items = new List<StoredObjectInfo>();
        await foreach (var blob in _container.GetBlobsAsync(prefix: withSlash, cancellationToken: cancellationToken))
        {
            var name = blob.Name;
            var relative = name.StartsWith(withSlash, StringComparison.OrdinalIgnoreCase)
                ? name[withSlash.Length..]
                : Path.GetFileName(name);
            if (string.IsNullOrWhiteSpace(relative))
            {
                continue;
            }

            var placeholder = string.Equals(relative, ConnectionPath.AzureKeepBlobName, StringComparison.OrdinalIgnoreCase)
                || relative.EndsWith("/" + ConnectionPath.AzureKeepBlobName, StringComparison.OrdinalIgnoreCase);
            items.Add(new StoredObjectInfo(
                name,
                relative,
                blob.Properties.ContentLength ?? 0,
                blob.Properties.LastModified?.UtcDateTime ?? DateTime.UnixEpoch,
                placeholder));
        }

        return items;
    }

    public async Task<string> SaveUnderPrefixAsync(
        string prefix,
        string relativeName,
        Stream content,
        string contentType,
        DateTimeOffset? lastWriteUtc = null,
        CancellationToken cancellationToken = default)
    {
        await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        var safePrefix = prefix.Replace('\\', '/').Trim().Trim('/');
        var safeName = relativeName.Replace('\\', '/').TrimStart('/');
        var blobName = $"{safePrefix}/{safeName}";
        var blob = _container.GetBlobClient(blobName);
        var headers = new BlobHttpHeaders { ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType };
        await blob.UploadAsync(content, new BlobUploadOptions { HttpHeaders = headers }, cancellationToken);
        return blobName;
    }
}

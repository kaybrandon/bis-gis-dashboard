using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using GisDashboard.Application.Abstractions;
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
}

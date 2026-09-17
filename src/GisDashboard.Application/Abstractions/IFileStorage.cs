namespace GisDashboard.Application.Abstractions;

public interface IFileStorage
{
    Task<string> SaveAsync(
        Guid organizationId,
        Guid workItemId,
        string fileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string blobPath, CancellationToken cancellationToken = default);

    Task<string> SaveRawAsync(
        string relativePath,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<StorageProbe> ProbeAsync(CancellationToken cancellationToken = default);
}

public sealed record StorageProbe(bool Ok, string Provider, string Detail, string? Error);

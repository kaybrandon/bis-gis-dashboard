namespace GisDashboard.Application.Abstractions;

public interface IFileStorage
{
    string ProviderLabel { get; }
    string ContainerName { get; }

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

    Task<StoragePrefixProbe> ProbePrefixAsync(string prefix, CancellationToken cancellationToken = default);

    Task EnsurePrefixAsync(string prefix, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredObjectInfo>> ListPrefixAsync(string prefix, CancellationToken cancellationToken = default);

    Task<string> SaveUnderPrefixAsync(
        string prefix,
        string relativeName,
        Stream content,
        string contentType,
        DateTimeOffset? lastWriteUtc = null,
        CancellationToken cancellationToken = default);
}

public sealed record StorageProbe(bool Ok, string Provider, string Detail, string? Error);

public sealed record StoragePrefixProbe(
    bool Ok,
    bool Listable,
    int FileCount,
    string? ErrorCode,
    string? Error,
    string Detail);

public sealed record StoredObjectInfo(
    string Name,
    string RelativeName,
    long Length,
    DateTimeOffset LastWriteUtc,
    bool IsPlaceholder);

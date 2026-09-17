namespace GisDashboard.Application.Connections;

public interface IConnectionService
{
    Task<IReadOnlyList<FileConnectionDto>> ListFileConnectionsAsync(CancellationToken cancellationToken = default);
    Task<FileConnectionDto> GetFileConnectionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FileConnectionDto> CreateFileConnectionAsync(UpsertFileConnectionRequest request, CancellationToken cancellationToken = default);
    Task<FileConnectionDto> UpdateFileConnectionAsync(Guid id, UpsertFileConnectionRequest request, CancellationToken cancellationToken = default);
    Task<FileConnectionDto> RunFileConnectionNowAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LanConnectionDto>> ListLanConnectionsAsync(CancellationToken cancellationToken = default);
    Task<LanConnectionDto> GetLanConnectionAsync(Guid id, bool revealToken = false, CancellationToken cancellationToken = default);
    Task<LanConnectionDto> CreateLanConnectionAsync(UpsertLanConnectionRequest request, CancellationToken cancellationToken = default);
    Task<LanConnectionDto> UpdateLanConnectionAsync(Guid id, UpsertLanConnectionRequest request, CancellationToken cancellationToken = default);
    Task<LanConnectionDto> RunLanNowAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LanConnectionDto> RetryLanAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LanConnectionDto> RotateLanTokenAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CheckFoldersResponse> CheckLanFoldersAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CheckFoldersResponse> CheckFoldersAsync(CheckFoldersRequest request, CancellationToken cancellationToken = default);
    Task<LanConnectionDto> AgentHeartbeatAsync(AgentHeartbeatRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FileServerDto>> ListFileServersAsync(CancellationToken cancellationToken = default);
    Task<FileServerDto> CreateFileServerAsync(string name, string rootPath, CancellationToken cancellationToken = default);

    Task<SyncControlDto> GetSyncControlAsync(CancellationToken cancellationToken = default);
    Task<SyncControlDto> SetPausedAsync(bool paused, CancellationToken cancellationToken = default);

    Task<FileKindsResponse> GetFileKindsAsync(CancellationToken cancellationToken = default);
}

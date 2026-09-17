using System.Security.Cryptography;
using System.Text;
using GisDashboard.Application.Abstractions;
using GisDashboard.Application.Connections;
using GisDashboard.Application.Exceptions;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Services;

public sealed class ConnectionService : IConnectionService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IFileStorage _storage;

    public ConnectionService(AppDbContext db, ICurrentUser currentUser, IFileStorage storage)
    {
        _db = db;
        _currentUser = currentUser;
        _storage = storage;
    }

    public async Task<IReadOnlyList<FileConnectionDto>> ListFileConnectionsAsync(CancellationToken cancellationToken = default)
    {
        EnsureCanSee();
        var rows = await _db.FileConnections.AsNoTracking()
            .Include(x => x.Organization)
            .Include(x => x.FileServer)
            .OrderBy(x => x.Organization!.Name)
            .ToListAsync(cancellationToken);
        return rows.Select(MapFile).ToList();
    }

    public async Task<FileConnectionDto> GetFileConnectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureCanSee();
        return MapFile(await LoadFileAsync(id, cancellationToken));
    }

    public async Task<FileConnectionDto> CreateFileConnectionAsync(UpsertFileConnectionRequest request, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();
        var org = await LoadOrgAsync(request.OrganizationId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var row = new FileConnection
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            FileServerId = request.FileServerId,
            SourcePath = ConnectionPath.Canonicalize(RequiredPath(request.SourcePath, "Source")),
            FtpFolder = NullIfEmpty(request.FtpFolder),
            FtpUrl = NullIfEmpty(request.FtpUrl),
            FtpUserName = NullIfEmpty(request.FtpUserName),
            FtpPasswordProtected = NullIfEmpty(request.FtpPassword),
            Enabled = request.Enabled ?? true,
            Status = "Idle",
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.FileConnections.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return MapFile(await LoadFileAsync(row.Id, cancellationToken));
    }

    public async Task<FileConnectionDto> UpdateFileConnectionAsync(Guid id, UpsertFileConnectionRequest request, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();
        var row = await LoadFileAsync(id, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.SourcePath))
        {
            row.SourcePath = ConnectionPath.Canonicalize(request.SourcePath);
        }

        if (request.FileServerId.HasValue)
        {
            row.FileServerId = request.FileServerId;
        }

        if (request.FtpFolder is not null)
        {
            row.FtpFolder = NullIfEmpty(request.FtpFolder);
        }

        if (request.FtpUrl is not null)
        {
            row.FtpUrl = NullIfEmpty(request.FtpUrl);
        }

        if (request.FtpUserName is not null)
        {
            row.FtpUserName = NullIfEmpty(request.FtpUserName);
        }

        if (!string.IsNullOrWhiteSpace(request.FtpPassword))
        {
            row.FtpPasswordProtected = request.FtpPassword;
        }

        if (request.Enabled.HasValue)
        {
            row.Enabled = request.Enabled.Value;
        }

        row.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapFile(await LoadFileAsync(row.Id, cancellationToken));
    }

    public Task<FileConnectionDto> RunFileConnectionNowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();
        throw new ValidationException("Add an agent before Run now. FTP publish is not used for Azure workfiles/orgs/ folders.");
    }

    public async Task<IReadOnlyList<LanConnectionDto>> ListLanConnectionsAsync(CancellationToken cancellationToken = default)
    {
        EnsureCanSee();
        var rows = await _db.LanConnections.AsNoTracking()
            .Include(x => x.Organization)
            .OrderBy(x => x.Organization!.Name)
            .ToListAsync(cancellationToken);
        return rows.Select(x => MapLan(x, enrollToken: null)).ToList();
    }

    public async Task<LanConnectionDto> GetLanConnectionAsync(Guid id, bool revealToken = false, CancellationToken cancellationToken = default)
    {
        EnsureCanSee();
        return MapLan(await LoadLanAsync(id, cancellationToken), revealToken ? null : null);
    }

    public async Task<LanConnectionDto> CreateLanConnectionAsync(UpsertLanConnectionRequest request, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();
        var org = await LoadOrgAsync(request.OrganizationId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var token = NewEnrollToken();
        var row = new LanConnection
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            BisFolder = ConnectionPath.Canonicalize(RequiredPath(request.BisFolder, "Source")),
            RemoteFolder = ConnectionPath.Canonicalize(RequiredPath(request.RemoteFolder, "Destination")),
            Direction = NormalizeDirection(request.Direction),
            ScheduleMinutes = request.ScheduleMinutes is > 0 ? request.ScheduleMinutes.Value : 15,
            EnrollTokenHash = HashToken(token),
            EnrollTokenMasked = MaskToken(token),
            Status = "Idle",
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.LanConnections.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return MapLan(await LoadLanAsync(row.Id, cancellationToken), token);
    }

    public async Task<LanConnectionDto> UpdateLanConnectionAsync(Guid id, UpsertLanConnectionRequest request, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();
        var row = await LoadLanAsync(id, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.BisFolder))
        {
            row.BisFolder = ConnectionPath.Canonicalize(request.BisFolder);
        }

        if (!string.IsNullOrWhiteSpace(request.RemoteFolder))
        {
            row.RemoteFolder = ConnectionPath.Canonicalize(request.RemoteFolder);
        }

        if (!string.IsNullOrWhiteSpace(request.Direction))
        {
            row.Direction = NormalizeDirection(request.Direction);
        }

        if (request.ScheduleMinutes is > 0)
        {
            row.ScheduleMinutes = request.ScheduleMinutes.Value;
        }

        row.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapLan(await LoadLanAsync(row.Id, cancellationToken), null);
    }

    public async Task<LanConnectionDto> RunLanNowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();
        await EnsureNotPausedAsync(cancellationToken);
        var row = await LoadLanAsync(id, cancellationToken);
        await SyncLanAsync(row, cancellationToken);
        return MapLan(await LoadLanAsync(row.Id, cancellationToken), null);
    }

    public Task<LanConnectionDto> RetryLanAsync(Guid id, CancellationToken cancellationToken = default) =>
        RunLanNowAsync(id, cancellationToken);

    public async Task<LanConnectionDto> RotateLanTokenAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();
        var row = await LoadLanAsync(id, cancellationToken);
        var token = NewEnrollToken();
        row.EnrollTokenHash = HashToken(token);
        row.EnrollTokenMasked = MaskToken(token);
        row.Enrolled = false;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapLan(await LoadLanAsync(row.Id, cancellationToken), token);
    }

    public async Task<CheckFoldersResponse> CheckLanFoldersAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureCanSee();
        var row = await LoadLanAsync(id, cancellationToken);
        return await CheckFoldersAsync(
            new CheckFoldersRequest(row.BisFolder, row.RemoteFolder, null),
            row,
            cancellationToken);
    }

    public Task<CheckFoldersResponse> CheckFoldersAsync(CheckFoldersRequest request, CancellationToken cancellationToken = default)
    {
        EnsureCanSee();
        return CheckFoldersAsync(request, agent: null, cancellationToken);
    }

    public async Task<LanConnectionDto> AgentHeartbeatAsync(AgentHeartbeatRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.EnrollToken))
        {
            throw new ValidationException("Enroll token is required.");
        }

        var hash = HashToken(request.EnrollToken.Trim());
        var row = await _db.LanConnections.FirstOrDefaultAsync(x => x.EnrollTokenHash == hash, cancellationToken)
            ?? throw new ForbiddenException("Enroll token is not valid.");

        row.Enrolled = true;
        row.HeartbeatOk = true;
        row.LastHeartbeatAt = DateTimeOffset.UtcNow;
        row.MachineName = NullIfEmpty(request.MachineName) ?? row.MachineName;
        row.WindowsUserName = FirstNonEmpty(request.WindowsUserName, request.UserName, request.User) ?? row.WindowsUserName;
        row.LocalIp = NullIfEmpty(request.LocalIp) ?? row.LocalIp;
        row.PublicIp = NullIfEmpty(request.PublicIp) ?? row.PublicIp;
        row.AgentVersion = NullIfEmpty(request.AgentVersion) ?? row.AgentVersion;
        row.HostName = NullIfEmpty(request.HostName) ?? row.HostName;
        row.OsDescription = NullIfEmpty(request.OsDescription) ?? row.OsDescription;
        row.OsVersion = NullIfEmpty(request.OsVersion) ?? row.OsVersion;
        row.Arch = NullIfEmpty(request.Arch) ?? row.Arch;
        row.RuntimeVersion = NullIfEmpty(request.RuntimeVersion) ?? row.RuntimeVersion;
        row.FreeDiskBytes = request.FreeDiskBytes ?? row.FreeDiskBytes;
        var sourceExists = request.SourceExists ?? request.SourceExistsOnAgent;
        if (sourceExists.HasValue)
        {
            row.SourceExistsOnAgent = sourceExists.Value;
        }

        var destinationExists = request.DestinationExists ?? request.DestinationExistsOnAgent ?? request.DestExists;
        if (destinationExists.HasValue)
        {
            row.DestinationExistsOnAgent = destinationExists.Value;
        }

        if (request.LocalFileCount.HasValue)
        {
            row.AgentLocalFileCount = request.LocalFileCount.Value;
        }

        if (request.LastPullCount.HasValue || request.LastPushCount.HasValue)
        {
            row.LastPullCount = request.LastPullCount ?? row.LastPullCount;
            row.LastPushCount = request.LastPushCount ?? row.LastPushCount;
            row.LastSyncAt = DateTimeOffset.UtcNow;
            row.RunNowQueued = false;
        }

        if (!string.IsNullOrWhiteSpace(request.LastError))
        {
            SetError(row, request.LastErrorCode ?? SyncErrorCodes.UploadFailed, request.LastError);
        }
        else if (request.LastPullCount.HasValue || request.LastPushCount.HasValue)
        {
            ClearError(row);
        }
        else if (row.Status == "Idle" || row.Status == "Offline")
        {
            row.Status = "Online";
        }

        row.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapLan(await LoadLanAsync(row.Id, cancellationToken), null);
    }

    public async Task<IReadOnlyList<FileServerDto>> ListFileServersAsync(CancellationToken cancellationToken = default)
    {
        EnsureCanSee();
        var rows = await _db.FileServers.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return rows.Select(x => new FileServerDto(x.Id, x.Name, x.RootPath, x.RootPath)).ToList();
    }

    public async Task<FileServerDto> CreateFileServerAsync(string name, string rootPath, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();
        var row = new FileServer
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(name) ? "File server" : name.Trim(),
            RootPath = (rootPath ?? string.Empty).Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.FileServers.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return new FileServerDto(row.Id, row.Name, row.RootPath, row.RootPath);
    }

    public async Task<SyncControlDto> GetSyncControlAsync(CancellationToken cancellationToken = default)
    {
        EnsureCanSee();
        var row = await EnsureSyncControlAsync(cancellationToken);
        return new SyncControlDto(row.Paused, row.Message);
    }

    public async Task<SyncControlDto> SetPausedAsync(bool paused, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();
        var row = await EnsureSyncControlAsync(cancellationToken);
        row.Paused = paused;
        row.Message = paused
            ? "Stops agent sync and FTP publish (nightly and Run now). Does not uninstall agents."
            : null;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return new SyncControlDto(row.Paused, row.Message);
    }

    public Task<FileKindsResponse> GetFileKindsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureCanSee();
        return Task.FromResult(new FileKindsResponse(
        [
            new FileKindDto("pdf", "PDF", ".pdf", true),
            new FileKindDto("shapefile", "Shapefile set", ".shp,.dbf,.shx,.prj,.cpg,.sbn,.sbx", true)
        ]));
    }

    private async Task<CheckFoldersResponse> CheckFoldersAsync(
        CheckFoldersRequest request,
        LanConnection? agent,
        CancellationToken cancellationToken)
    {
        var sourcePath = request.SourcePath?.Trim() ?? string.Empty;
        var destPath = request.RemoteFolder?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destPath))
        {
            throw new ValidationException("Source and Destination are required.");
        }

        var source = await ProbeSideAsync("Source", sourcePath, agent, isSource: true, cancellationToken);
        var dest = await ProbeSideAsync("Destination", destPath, agent, isSource: false, cancellationToken);
        return new CheckFoldersResponse(source, dest, source.Ok && dest.Ok, source.Ok && dest.Ok);
    }

    private async Task<FolderCheckResult> ProbeSideAsync(
        string label,
        string path,
        LanConnection? agent,
        bool isSource,
        CancellationToken cancellationToken)
    {
        if (ConnectionPath.IsAzureOrgPath(path))
        {
            var prefix = ConnectionPath.ToAzurePrefix(path);
            var probe = await _storage.ProbePrefixAsync(prefix, cancellationToken);
            var shown = ConnectionPath.DisplayPath(path);
            if (probe.Ok && probe.Listable)
            {
                return new FolderCheckResult(
                    true,
                    "Pass",
                    $"{label} Azure folder {shown} is reachable ({probe.FileCount} file{(probe.FileCount == 1 ? "" : "s")}).",
                    "azure");
            }

            return new FolderCheckResult(
                false,
                "Fail",
                AzureFailMessage(label, shown, probe.Error),
                "azure");
        }

        var kind = ConnectionPath.IsUncPath(path) ? "unc" : "local";
        if (AgentReportedExists(agent, isSource, path))
        {
            return new FolderCheckResult(
                true,
                "Pass",
                $"{label} folder {path} was confirmed by the agent as {AgentWho(agent)}.",
                kind);
        }

        if (TryLocalFolder(path, out var full, out var hostError))
        {
            var count = Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories).Count();
            return new FolderCheckResult(
                true,
                "Pass",
                $"{label} folder {path} is reachable ({count} file{(count == 1 ? "" : "s")}) for {ConnectionPath.CheckerIdentity()}.",
                kind);
        }

        if (AgentIsAssigned(agent))
        {
            return new FolderCheckResult(
                false,
                "Fail",
                AgentUnconfirmedMessage(label, path, agent),
                kind);
        }

        var message = hostError
            ?? (ConnectionPath.IsUncPath(path)
                ? $"{label} UNC share was not reachable. Use a PC local drive path (C:\\...) unless this share is reachable from the agent. Checked as {ConnectionPath.CheckerIdentity()}."
                : $"{label} folder was not found on the PC or file server: {path}. Checked as {ConnectionPath.CheckerIdentity()}.");
        return new FolderCheckResult(false, "Fail", message, kind);
    }

    private async Task SyncLanAsync(LanConnection row, CancellationToken cancellationToken)
    {
        var check = await CheckFoldersAsync(
            new CheckFoldersRequest(row.BisFolder, row.RemoteFolder, null),
            row,
            cancellationToken);
        if (!check.Source.Ok || !check.Destination.Ok)
        {
            var fail = !check.Source.Ok ? check.Source : check.Destination;
            if (fail.Kind == "azure")
            {
                var code = fail.Message.Contains("permission", StringComparison.OrdinalIgnoreCase)
                    ? SyncErrorCodes.AzurePrefixForbidden
                    : SyncErrorCodes.AzurePrefixNotFound;
                SetError(row, code, fail.Message);
                await _db.SaveChangesAsync(cancellationToken);
                return;
            }

            var failedPath = !check.Destination.Ok ? row.RemoteFolder : row.BisFolder;
            var agentOwnsLocal = AgentIsAssigned(row)
                && (ConnectionPath.IsLocalDrivePath(failedPath) || ConnectionPath.IsUncPath(failedPath));
            if (agentOwnsLocal)
            {
                row.RunNowQueued = true;
                SetError(row, SyncErrorCodes.CheckNotOnAgent, fail.Message);
                await _db.SaveChangesAsync(cancellationToken);
                return;
            }

            SetError(row, SyncErrorCodes.PathNotFound, fail.Message);
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var source = await ResolveSideAsync(row.BisFolder, row, isSource: true, ensure: false, cancellationToken);
        var dest = await ResolveSideAsync(row.RemoteFolder, row, isSource: false, ensure: true, cancellationToken);
        if (source.Error is not null)
        {
            SetError(row, source.ErrorCode ?? SyncErrorCodes.PathNotFound, source.Error);
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (dest.Error is not null)
        {
            SetError(row, dest.ErrorCode ?? SyncErrorCodes.PathNotFound, dest.Error);
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (source.Kind == SideKind.RemoteAgent || dest.Kind == SideKind.RemoteAgent)
        {
            var localCount = LocalFileCount(source) + LocalFileCount(dest) + row.AgentLocalFileCount;
            var azureEmpty = (source.Kind == SideKind.Azure && source.Files.Count == 0)
                || (dest.Kind == SideKind.Azure && dest.Files.Count == 0);
            var needsUpload = NeedsCopyToAzure(row.Direction, source, dest);
            if (needsUpload && azureEmpty && localCount == 0)
            {
                SetError(
                    row,
                    SyncErrorCodes.EmptyRemoteLocalHasFiles,
                    $"Bidirectional sync did not write. Azure folder {AzureDisplay(source, dest)} is empty and no PC files were available to upload.");
                await _db.SaveChangesAsync(cancellationToken);
                return;
            }

            row.RunNowQueued = true;
            SetError(
                row,
                SyncErrorCodes.AgentUploadPending,
                $"Agent must copy files for {row.Direction}. Azure folder {AzureDisplay(source, dest)} was checked. Run now is queued — not a silent success.");
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var push = 0;
        var pull = 0;
        try
        {
            if (row.Direction is "Push" or "Bidirectional")
            {
                push = await CopyAsync(source, dest, cancellationToken);
            }

            if (row.Direction is "Pull" or "Bidirectional")
            {
                pull = await CopyAsync(dest, source, cancellationToken);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            SetError(row, SyncErrorCodes.PermissionDenied, $"Permission error while syncing: {ex.Message}");
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }
        catch (IOException ex)
        {
            SetError(row, dest.Kind == SideKind.Azure ? SyncErrorCodes.AzureWriteFailed : SyncErrorCodes.UploadFailed, ex.Message);
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        row.LastPushCount = push;
        row.LastPullCount = pull;
        row.LastSyncAt = DateTimeOffset.UtcNow;
        row.RunNowQueued = false;

        var localFiles = LocalFileCount(source) + LocalFileCount(dest);
        var copiedTowardAzure = CopiedTowardAzure(row.Direction, source, dest, push, pull);
        var azureStillEmpty = await AzureEmptyAsync(source, dest, cancellationToken);
        if (NeedsCopyToAzure(row.Direction, source, dest) && localFiles > 0 && copiedTowardAzure == 0)
        {
            SetError(
                row,
                SyncErrorCodes.EmptyRemoteLocalHasFiles,
                $"Local files were not uploaded to Azure folder {AzureDisplay(source, dest)}. Pull {pull} / Push {push}.");
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (azureStillEmpty && localFiles > 0 && NeedsCopyToAzure(row.Direction, source, dest))
        {
            SetError(
                row,
                SyncErrorCodes.EmptyRemoteLocalHasFiles,
                $"Azure folder {AzureDisplay(source, dest)} is still empty after sync. Local files should have uploaded.");
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        ClearError(row);
        row.Status = row.Enrolled || row.HeartbeatOk ? "Online" : "Idle";
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<ResolvedSide> ResolveSideAsync(
        string path,
        LanConnection agent,
        bool isSource,
        bool ensure,
        CancellationToken cancellationToken)
    {
        if (ConnectionPath.IsAzureOrgPath(path))
        {
            var prefix = ConnectionPath.ToAzurePrefix(path);
            if (ensure)
            {
                try
                {
                    await _storage.EnsurePrefixAsync(prefix, cancellationToken);
                }
                catch (Exception ex)
                {
                    return new ResolvedSide(
                        SideKind.Azure,
                        path,
                        prefix,
                        null,
                        [],
                        SyncErrorCodes.AzureWriteFailed,
                        $"Could not write to Azure folder {ConnectionPath.DisplayPath(path)}. {ex.Message}");
                }
            }

            var probe = await _storage.ProbePrefixAsync(prefix, cancellationToken);
            if (!probe.Ok)
            {
                return new ResolvedSide(SideKind.Azure, path, prefix, null, [], probe.ErrorCode, probe.Error);
            }

            var files = await _storage.ListPrefixAsync(prefix, cancellationToken);
            return new ResolvedSide(SideKind.Azure, path, prefix, null, files.Where(x => !x.IsPlaceholder).ToList(), null, null);
        }

        if (TryLocalFolder(path, out var full, out var error))
        {
            if (ensure)
            {
                Directory.CreateDirectory(full);
            }

            var files = Directory.Exists(full)
                ? Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories)
                    .Select(file =>
                    {
                        var info = new FileInfo(file);
                        return new StoredObjectInfo(
                            file,
                            Path.GetRelativePath(full, file).Replace('\\', '/'),
                            info.Length,
                            info.LastWriteTimeUtc,
                            false);
                    })
                    .ToList()
                : [];
            return new ResolvedSide(SideKind.Local, path, null, full, files, null, null);
        }

        var agentOk = isSource ? agent.SourceExistsOnAgent : agent.DestinationExistsOnAgent;
        if (agentOk || agent.Enrolled)
        {
            return new ResolvedSide(SideKind.RemoteAgent, path, null, null, [], null, null);
        }

        return new ResolvedSide(
            SideKind.Local,
            path,
            null,
            null,
            [],
            SyncErrorCodes.PathNotFound,
            error ?? $"Folder was not found on the PC or file server: {path}");
    }

    private async Task<int> CopyAsync(ResolvedSide from, ResolvedSide to, CancellationToken cancellationToken)
    {
        if (from.Kind != SideKind.Azure && from.Kind != SideKind.Local)
        {
            return 0;
        }

        if (to.Kind != SideKind.Azure && to.Kind != SideKind.Local)
        {
            return 0;
        }

        if (to.Kind == SideKind.Azure && to.Prefix is not null)
        {
            await _storage.EnsurePrefixAsync(to.Prefix, cancellationToken);
        }

        if (to.Kind == SideKind.Local && to.LocalPath is not null)
        {
            Directory.CreateDirectory(to.LocalPath);
        }

        var destByName = to.Files.ToDictionary(x => x.RelativeName, StringComparer.OrdinalIgnoreCase);
        var copied = 0;
        foreach (var file in from.Files)
        {
            if (!destByName.TryGetValue(file.RelativeName, out var existing)
                || file.LastWriteUtc > existing.LastWriteUtc)
            {
                await CopyOneAsync(from, to, file, cancellationToken);
                copied++;
            }
        }

        return copied;
    }

    private async Task CopyOneAsync(ResolvedSide from, ResolvedSide to, StoredObjectInfo file, CancellationToken cancellationToken)
    {
        if (from.Kind == SideKind.Azure)
        {
            await using var stream = await _storage.OpenReadAsync(file.Name, cancellationToken);
            if (to.Kind == SideKind.Azure)
            {
                await _storage.SaveUnderPrefixAsync(to.Prefix!, file.RelativeName, stream, "application/octet-stream", file.LastWriteUtc, cancellationToken);
                return;
            }

            var dest = Path.Combine(to.LocalPath!, file.RelativeName.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            await using var output = File.Create(dest);
            await stream.CopyToAsync(output, cancellationToken);
            return;
        }

        await using var local = File.OpenRead(file.Name);
        if (to.Kind == SideKind.Azure)
        {
            await _storage.SaveUnderPrefixAsync(to.Prefix!, file.RelativeName, local, "application/octet-stream", file.LastWriteUtc, cancellationToken);
            return;
        }

        var target = Path.Combine(to.LocalPath!, file.RelativeName.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await using var copy = File.Create(target);
        await local.CopyToAsync(copy, cancellationToken);
    }

    private async Task<bool> AzureEmptyAsync(ResolvedSide source, ResolvedSide dest, CancellationToken cancellationToken)
    {
        foreach (var side in new[] { source, dest })
        {
            if (side.Kind != SideKind.Azure || side.Prefix is null)
            {
                continue;
            }

            var files = await _storage.ListPrefixAsync(side.Prefix, cancellationToken);
            if (files.Any(x => !x.IsPlaceholder))
            {
                return false;
            }

            return true;
        }

        return false;
    }

    private static bool NeedsCopyToAzure(string direction, ResolvedSide source, ResolvedSide dest) =>
        (direction is "Push" or "Bidirectional" && dest.Kind == SideKind.Azure)
        || (direction is "Pull" or "Bidirectional" && source.Kind == SideKind.Azure);

    private static int CopiedTowardAzure(string direction, ResolvedSide source, ResolvedSide dest, int push, int pull)
    {
        var count = 0;
        if (dest.Kind == SideKind.Azure && direction is "Push" or "Bidirectional")
        {
            count += push;
        }

        if (source.Kind == SideKind.Azure && direction is "Pull" or "Bidirectional")
        {
            count += pull;
        }

        return count;
    }

    private static int LocalFileCount(ResolvedSide side) =>
        side.Kind == SideKind.Local ? side.Files.Count : 0;

    private static string AzureDisplay(ResolvedSide source, ResolvedSide dest)
    {
        if (source.Kind == SideKind.Azure)
        {
            return ConnectionPath.DisplayPath(source.Path);
        }

        if (dest.Kind == SideKind.Azure)
        {
            return ConnectionPath.DisplayPath(dest.Path);
        }

        return "workfiles/orgs/…";
    }

    private static bool TryLocalFolder(string path, out string full, out string? error)
    {
        full = path;
        error = null;
        if (ConnectionPath.IsAzureOrgPath(path))
        {
            error = "Azure workfiles/orgs/ paths are not PC or file-server folders.";
            return false;
        }

        try
        {
            if (Directory.Exists(path))
            {
                full = path;
                return true;
            }

            error = ConnectionPath.IsUncPath(path)
                ? $"UNC share was not reachable: {path}. Use a PC local drive path (C:\\...) unless this share is reachable from the agent."
                : $"Folder was not found on the PC or file server: {path}";
            return false;
        }
        catch (Exception ex)
        {
            error = $"Could not open folder {path}. {ex.Message}";
            return false;
        }
    }

    private static void SetError(LanConnection row, string code, string message)
    {
        row.Status = "Error";
        row.LastErrorCode = code;
        row.LastError = message;
        row.LastErrorAt = DateTimeOffset.UtcNow;
        row.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void ClearError(LanConnection row)
    {
        row.LastError = null;
        row.LastErrorCode = null;
        row.LastErrorAt = null;
        row.Status = row.Enrolled || row.HeartbeatOk ? "Online" : "Idle";
        row.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private async Task EnsureNotPausedAsync(CancellationToken cancellationToken)
    {
        var control = await EnsureSyncControlAsync(cancellationToken);
        if (control.Paused)
        {
            throw new ValidationException("All sync is paused.");
        }
    }

    private async Task<SyncControlState> EnsureSyncControlAsync(CancellationToken cancellationToken)
    {
        var row = await _db.SyncControl.FirstOrDefaultAsync(x => x.Id == SyncControlState.SingletonId, cancellationToken);
        if (row is not null)
        {
            return row;
        }

        row = new SyncControlState { Id = SyncControlState.SingletonId, UpdatedAt = DateTimeOffset.UtcNow };
        _db.SyncControl.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return row;
    }

    private async Task<Organization> LoadOrgAsync(Guid? organizationId, CancellationToken cancellationToken)
    {
        if (organizationId is null || organizationId == Guid.Empty)
        {
            throw new ValidationException("Select a client.");
        }

        return await _db.Organizations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == organizationId, cancellationToken)
            ?? throw new NotFoundException("Organization was not found.");
    }

    private async Task<FileConnection> LoadFileAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.FileConnections
            .Include(x => x.Organization)
            .Include(x => x.FileServer)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Connection was not found.");

    private async Task<LanConnection> LoadLanAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.LanConnections
            .Include(x => x.Organization)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Connection was not found.");

    private FileConnectionDto MapFile(FileConnection row) =>
        new(
            row.Id,
            row.OrganizationId,
            row.Organization?.Name ?? "Client",
            row.FileServerId,
            row.FileServer?.RootPath,
            row.FileServer?.RootPath,
            ConnectionPath.Canonicalize(row.SourcePath),
            row.FtpFolder,
            row.FtpUrl,
            row.FtpUserName,
            !string.IsNullOrWhiteSpace(row.FtpPasswordProtected),
            row.Enabled,
            row.Status,
            row.LastError,
            row.LastPublishedAt,
            row.LastFileCount,
            row.LastZipName,
            false,
            false,
            CanManage);

    private LanConnectionDto MapLan(LanConnection row, string? enrollToken) =>
        new(
            row.Id,
            row.OrganizationId,
            row.Organization?.Name ?? "Client",
            ConnectionPath.Canonicalize(row.RemoteFolder),
            ConnectionPath.Canonicalize(row.BisFolder),
            row.Direction,
            row.ScheduleMinutes,
            row.Enrolled,
            enrollToken,
            row.EnrollTokenMasked,
            AgentUiStatus(row),
            ConnectionPath.HeartbeatIsFresh(row.LastHeartbeatAt) || (row.HeartbeatOk && row.LastHeartbeatAt.HasValue),
            row.LastHeartbeatAt,
            row.LastSyncAt,
            row.LastPullCount,
            row.LastPushCount,
            row.LastError,
            row.LastErrorCode,
            row.LastErrorAt,
            row.MachineName,
            row.WindowsUserName,
            ConnectionPath.HeartbeatLabel(row.LastHeartbeatAt, row.Enrolled),
            ConnectionPath.HeartbeatIsFresh(row.LastHeartbeatAt),
            row.LocalIp,
            row.PublicIp,
            row.AgentVersion,
            new AgentTelemetryDto(
                row.HostName,
                row.OsDescription,
                row.OsVersion,
                row.Arch,
                row.RuntimeVersion,
                row.AgentVersion,
                row.FreeDiskBytes,
                row.LastError,
                row.LocalIp,
                row.PublicIp,
                row.WindowsUserName),
            row.RestartPending,
            false,
            row.RestartResult,
            row.UpdatePending,
            false,
            row.UpdateTargetVersion,
            row.UpdateStatus,
            null,
            row.RunNowQueued,
            CanManage);

    private static string AgentUiStatus(LanConnection row)
    {
        if (!string.IsNullOrWhiteSpace(row.LastError) || !string.IsNullOrWhiteSpace(row.LastErrorCode) || row.Status == "Error")
        {
            return "Error";
        }

        if (row.Status == "Offline")
        {
            return "Offline";
        }

        if (ConnectionPath.HeartbeatIsFresh(row.LastHeartbeatAt)
            || (row.HeartbeatOk && row.LastHeartbeatAt.HasValue))
        {
            return "Online";
        }

        if (row.Status == "Online")
        {
            return "Idle";
        }

        return string.IsNullOrWhiteSpace(row.Status) ? "Idle" : row.Status;
    }

    private void EnsureCanSee()
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new ForbiddenException("Authentication is required.");
        }

        if (!_currentUser.CanSeeConnections)
        {
            throw new ForbiddenException("You do not have access to Connections.");
        }
    }

    private void EnsureCanManage()
    {
        EnsureCanSee();
        if (!_currentUser.CanManageConnections)
        {
            throw new ForbiddenException("You cannot change Connections.");
        }
    }

    private bool CanManage => _currentUser.CanManageConnections;

    private static string RequiredPath(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException($"Enter the {label} path");
        }

        return value.Trim();
    }

    private static string NormalizeDirection(string? value) =>
        value?.Trim() switch
        {
            "Push" => "Push",
            "Pull" => "Pull",
            _ => "Bidirectional"
        };

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NewEnrollToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()));
        return Convert.ToHexString(bytes);
    }

    private static string MaskToken(string token) =>
        token.Length <= 8 ? "••••••••••••" : token[..4] + "••••••••" + token[^2..];

    private static bool AgentIsAssigned(LanConnection? agent) =>
        agent is not null && (agent.Enrolled || agent.HeartbeatOk || agent.LastHeartbeatAt.HasValue);

    private static bool AgentReportedExists(LanConnection? agent, bool isSource, string path)
    {
        if (agent is null)
        {
            return false;
        }

        if (isSource ? agent.SourceExistsOnAgent : agent.DestinationExistsOnAgent)
        {
            return true;
        }

        if (agent.AgentLocalFileCount <= 0 || ConnectionPath.IsAzureOrgPath(path))
        {
            return false;
        }

        var other = isSource ? agent.RemoteFolder : agent.BisFolder;
        return ConnectionPath.IsAzureOrgPath(other);
    }

    private static string AgentWho(LanConnection? agent) =>
        ConnectionPath.AgentIdentity(agent?.WindowsUserName, agent?.MachineName, agent?.HostName);

    private static string AgentUnconfirmedMessage(string label, string path, LanConnection? agent)
    {
        var heartbeat = ConnectionPath.HeartbeatLabel(agent?.LastHeartbeatAt, agent?.Enrolled == true);
        var agentWho = AgentWho(agent);
        var apiWho = ConnectionPath.CheckerIdentity();
        return $"{label} folder {path} was not confirmed by the enrolled agent as {agentWho}. Last heartbeat: {heartbeat}. This Check ran as {apiWho} — not a PATH_NOT_FOUND on that PC.";
    }

    private static string AzureFailMessage(string label, string shown, string? probeError)
    {
        if (!string.IsNullOrWhiteSpace(probeError) && probeError.Contains("workfiles/", StringComparison.OrdinalIgnoreCase))
        {
            return probeError;
        }

        return probeError ?? $"{label} Azure folder {shown} was not found.";
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

    private enum SideKind
    {
        Azure,
        Local,
        RemoteAgent
    }

    private sealed record ResolvedSide(
        SideKind Kind,
        string Path,
        string? Prefix,
        string? LocalPath,
        IReadOnlyList<StoredObjectInfo> Files,
        string? ErrorCode,
        string? Error);
}

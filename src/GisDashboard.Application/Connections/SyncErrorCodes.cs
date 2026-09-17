namespace GisDashboard.Application.Connections;

public static class SyncErrorCodes
{
    public const string AzurePrefixNotFound = "AZURE_PREFIX_NOT_FOUND";
    public const string AzurePrefixForbidden = "AZURE_PREFIX_FORBIDDEN";
    public const string AzureWriteFailed = "AZURE_WRITE_FAILED";
    public const string PathNotFound = "PATH_NOT_FOUND";
    public const string PermissionDenied = "PERMISSION_DENIED";
    public const string UploadFailed = "UPLOAD_FAILED";
    public const string DownloadFailed = "DOWNLOAD_FAILED";
    public const string EmptyRemoteLocalHasFiles = "EMPTY_REMOTE_LOCAL_HAS_FILES";
    public const string AgentUploadPending = "AGENT_UPLOAD_PENDING";
}

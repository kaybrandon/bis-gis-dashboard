namespace GisDashboard.Domain;

public static class WorkItemAiScanStatus
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Unconfigured = "unconfigured";
    public const string Skipped = "skipped";

    public const string PendingMessage = "AI scan pending.";
    public const string UnconfiguredMessage =
        "AI scan failed — Azure OpenAI is not configured. Use AI fill from PDF after it is configured.";
    public const string FailedMessage = "AI scan failed. Use AI fill from PDF to retry.";
    public const string SucceededMessage = "AI scan finished. Review amber fields, then Save.";
    public const string SkippedMessage = "AI scan skipped — not a PDF.";

    public static bool IsPdf(string? fileName, string? contentType)
    {
        if (string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(fileName)
            && fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsInFlight(string? status) =>
        status is Pending or Running;

    public static bool IsSucceeded(string? status) =>
        string.Equals(status, Succeeded, StringComparison.OrdinalIgnoreCase);
}

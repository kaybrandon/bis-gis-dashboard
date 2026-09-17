namespace GisDashboard.Application.Email;

public sealed class SaveEmailSettingsRequest
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? From { get; set; }
    public string? FromName { get; set; }
    public string? User { get; set; }
    public string? Password { get; set; }
    public string? ReplyTo { get; set; }
}

public sealed class SendTestEmailRequest
{
    public string? To { get; set; }
}

public sealed record EmailSettingsDto(
    string Status,
    bool Configured,
    bool Enabled,
    string? Host,
    int Port,
    bool UseSsl,
    string? From,
    string? FromName,
    string? User,
    bool PasswordConfigured,
    string PasswordSource,
    string? ReplyTo,
    DateTimeOffset? UpdatedAt,
    string Note);

public sealed record EmailTestResult(bool Delivered, string Mode, string Recipients, string Note);

public sealed record ResolvedEmailSettings(
    bool Enabled,
    string? Host,
    int Port,
    bool UseSsl,
    string From,
    string FromName,
    string? User,
    string? Password,
    string? ReplyTo,
    bool PasswordConfigured,
    string PasswordSource)
{
    public bool IsConfigured =>
        Enabled && !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}

public interface IEmailSettingsService
{
    Task RefreshCacheAsync(CancellationToken cancellationToken = default);
    Task<EmailSettingsDto> GetAsync(CancellationToken cancellationToken = default);
    Task<EmailSettingsDto> SaveAsync(SaveEmailSettingsRequest request, CancellationToken cancellationToken = default);
    Task<EmailTestResult> SendTestAsync(SendTestEmailRequest request, CancellationToken cancellationToken = default);
}

public interface IEmailSettingsCache
{
    ResolvedEmailSettings Current { get; }
    void Replace(ResolvedEmailSettings settings);
}

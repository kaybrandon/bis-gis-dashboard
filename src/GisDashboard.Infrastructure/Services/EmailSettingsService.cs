using System.Text.RegularExpressions;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using GisDashboard.Application.Abstractions;
using GisDashboard.Application.Email;
using GisDashboard.Application.Exceptions;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Email;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GisDashboard.Infrastructure.Services;

public sealed class EmailSettingsService : IEmailSettingsService
{
    public const string UnconfiguredMessage =
        "Email is not configured. A Global Administrator can set SMTP under Admin Settings → Email / SMTP.";

    private static readonly Regex EmailPattern = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IEmailSettingsCache _cache;
    private readonly IEmailSender _email;
    private readonly IDataProtector _protector;
    private readonly IOptions<EmailOptions> _configOptions;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailSettingsService> _logger;

    public EmailSettingsService(
        AppDbContext db,
        ICurrentUser currentUser,
        IEmailSettingsCache cache,
        IEmailSender email,
        IDataProtectionProvider dataProtection,
        IOptions<EmailOptions> configOptions,
        IConfiguration configuration,
        ILogger<EmailSettingsService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _cache = cache;
        _email = email;
        _protector = dataProtection.CreateProtector("GisDashboard.Email.Smtp.Password");
        _configOptions = configOptions;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task RefreshCacheAsync(CancellationToken cancellationToken = default)
    {
        _cache.Replace(await ResolveAsync(cancellationToken));
    }

    public async Task<EmailSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        EnsureGlobalAdmin();
        await RefreshCacheAsync(cancellationToken);
        return ToDto(await LoadRowAsync(cancellationToken), _cache.Current);
    }

    public async Task<EmailSettingsDto> SaveAsync(SaveEmailSettingsRequest request, CancellationToken cancellationToken = default)
    {
        EnsureGlobalAdmin();

        var host = Trim(request.Host);
        var from = Trim(request.From);
        var fromName = Trim(request.FromName);
        var user = Trim(request.User);
        var replyTo = Trim(request.ReplyTo);
        var password = request.Password ?? string.Empty;

        if (request.Port is < 1 or > 65535)
        {
            throw new ValidationException("Port must be between 1 and 65535.");
        }

        if (!string.IsNullOrWhiteSpace(from) && !EmailPattern.IsMatch(from))
        {
            throw new ValidationException("From address is not a valid email.");
        }

        if (!string.IsNullOrWhiteSpace(replyTo) && !EmailPattern.IsMatch(replyTo))
        {
            throw new ValidationException("Reply-to is not a valid email.");
        }

        if (!string.IsNullOrWhiteSpace(host) && string.IsNullOrWhiteSpace(from))
        {
            throw new ValidationException("From address is required when an SMTP host is set.");
        }

        var row = await LoadRowAsync(cancellationToken) ?? new EmailSettings { Id = EmailSettings.SingletonId };
        row.Host = host;
        row.Port = request.Port;
        row.UseSsl = request.UseSsl;
        row.FromAddress = from;
        row.FromName = fromName;
        row.UserName = user;
        row.ReplyTo = replyTo;
        row.Enabled = !string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(from);
        row.UpdatedAt = DateTimeOffset.UtcNow;
        row.UpdatedByUserId = _currentUser.UserId == Guid.Empty ? null : _currentUser.UserId;

        if (!string.IsNullOrWhiteSpace(password))
        {
            row.PasswordProtected = _protector.Protect(password);
            await TryWriteKeyVaultPasswordAsync(password, cancellationToken);
        }

        if (_db.Entry(row).State == EntityState.Detached)
        {
            _db.EmailSettings.Add(row);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await RefreshCacheAsync(cancellationToken);
        return ToDto(row, _cache.Current);
    }

    public async Task<EmailTestResult> SendTestAsync(SendTestEmailRequest request, CancellationToken cancellationToken = default)
    {
        EnsureGlobalAdmin();
        await RefreshCacheAsync(cancellationToken);

        if (!_cache.Current.IsConfigured)
        {
            throw new ServiceUnavailableException(UnconfiguredMessage);
        }

        var to = Trim(request.To);
        if (string.IsNullOrWhiteSpace(to) || !EmailPattern.IsMatch(to))
        {
            throw new ValidationException("Enter a valid To address.");
        }

        var result = await _email.SendAsync(
            new OutboundEmail(
                [to],
                "GIS Dashboard — SMTP test",
                "<p>This is a test message from Admin Settings → Email / SMTP.</p>",
                "This is a test message from Admin Settings → Email / SMTP.",
                []),
            cancellationToken);

        if (!result.Delivered)
        {
            throw new ServiceUnavailableException(
                result.Error ?? (result.Mode == "dry-run" ? UnconfiguredMessage : "The test email was not delivered."));
        }

        return new EmailTestResult(true, result.Mode, to, $"Test email sent to {to}.");
    }

    private void EnsureGlobalAdmin()
    {
        if (!_currentUser.CanManageGlobalDirectory)
        {
            throw new ForbiddenException("Only a Global Administrator can manage email / SMTP.");
        }
    }

    private Task<EmailSettings?> LoadRowAsync(CancellationToken cancellationToken) =>
        _db.EmailSettings.FirstOrDefaultAsync(x => x.Id == EmailSettings.SingletonId, cancellationToken);

    private async Task<ResolvedEmailSettings> ResolveAsync(CancellationToken cancellationToken)
    {
        var config = _configOptions.Value;
        var row = await LoadRowAsync(cancellationToken);
        var host = First(row?.Host, config.Smtp.Host);
        var from = First(row?.FromAddress, config.From) ?? "noreply@bisconsultants.local";
        var fromName = First(row?.FromName, config.FromName) ?? "GIS Dashboard";
        var user = First(row?.UserName, config.Smtp.User);
        var replyTo = First(row?.ReplyTo, config.ReplyTo);
        var port = row is not null ? row.Port : (config.Smtp.Port > 0 ? config.Smtp.Port : 587);
        var useSsl = row?.UseSsl ?? config.Smtp.UseSsl;
        var enabled = row is not null ? row.Enabled : config.Enabled;

        var storePassword = Unprotect(row?.PasswordProtected);
        var configPassword = string.IsNullOrWhiteSpace(config.Smtp.Password) ? null : config.Smtp.Password;
        var password = storePassword ?? configPassword;
        var passwordConfigured = !string.IsNullOrWhiteSpace(password);
        var passwordSource = !string.IsNullOrWhiteSpace(storePassword)
            ? "secure-store"
            : !string.IsNullOrWhiteSpace(configPassword)
                ? (HasKeyVaultUri() ? "key-vault" : "app-settings")
                : "none";

        return new ResolvedEmailSettings(
            enabled,
            host,
            port,
            useSsl,
            from,
            fromName,
            user,
            password,
            replyTo,
            passwordConfigured,
            passwordSource);
    }

    private EmailSettingsDto ToDto(EmailSettings? row, ResolvedEmailSettings resolved)
    {
        var configured = resolved.IsConfigured;
        return new EmailSettingsDto(
            configured ? "configured" : "not_configured",
            configured,
            resolved.Enabled,
            resolved.Host,
            resolved.Port,
            resolved.UseSsl,
            resolved.From,
            resolved.FromName,
            resolved.User,
            resolved.PasswordConfigured,
            resolved.PasswordSource,
            resolved.ReplyTo,
            row?.UpdatedAt,
            configured
                ? "SMTP is configured. Dashboard Send report and other mail use these settings."
                : "SMTP is not configured. Save a host and From address. The password is write-only and is never returned.");
    }

    private string? Unprotect(string? cipher)
    {
        if (string.IsNullOrWhiteSpace(cipher))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(cipher);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not decrypt the stored SMTP password. Save the password again.");
            return null;
        }
    }

    private async Task TryWriteKeyVaultPasswordAsync(string password, CancellationToken cancellationToken)
    {
        var raw = _configuration["KeyVaultUri"] ?? _configuration["KeyVault:VaultUri"];
        if (string.IsNullOrWhiteSpace(raw) || !Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            return;
        }

        try
        {
            var client = new SecretClient(uri, new DefaultAzureCredential());
            await client.SetSecretAsync("Email--Smtp--Password", password, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not write Email--Smtp--Password to Key Vault. The password stays in the encrypted store.");
        }
    }

    private bool HasKeyVaultUri()
    {
        var raw = _configuration["KeyVaultUri"] ?? _configuration["KeyVault:VaultUri"];
        return !string.IsNullOrWhiteSpace(raw);
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? First(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}

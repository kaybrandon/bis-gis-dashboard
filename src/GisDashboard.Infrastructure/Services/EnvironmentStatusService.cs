using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using GisDashboard.Application.Abstractions;
using GisDashboard.Application.AiFill;
using GisDashboard.Application.Email;
using GisDashboard.Application.Exceptions;
using GisDashboard.Application.Status;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GisDashboard.Infrastructure.Services;

public sealed class EnvironmentStatusService : IEnvironmentStatusService
{
    private readonly ICurrentUser _currentUser;
    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly IEmailSettingsCache _email;
    private readonly AzureOpenAIOptions _openAi;
    private readonly DateTimeOffset _processStarted = ProcessStartUtc();

    public EnvironmentStatusService(
        ICurrentUser currentUser,
        AppDbContext db,
        IFileStorage storage,
        IConfiguration configuration,
        IHostEnvironment environment,
        IEmailSettingsCache email,
        IOptions<AzureOpenAIOptions> openAi)
    {
        _currentUser = currentUser;
        _db = db;
        _storage = storage;
        _configuration = configuration;
        _environment = environment;
        _email = email;
        _openAi = openAi.Value;
    }

    public async Task<EnvironmentStatusResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.CanManageGlobalDirectory)
        {
            throw new ForbiddenException("Only a Global Administrator can view environment status.");
        }

        var checks = new List<EnvironmentCheck>
        {
            ApiCheck(),
            await DatabaseCheckAsync(cancellationToken),
            await StorageCheckAsync(cancellationToken),
            await KeyVaultCheckAsync(cancellationToken),
            AppInsightsCheck(),
            EmailCheck(),
            AzureOpenAiCheck()
        };

        var live = checks.Where(c => c.Mode == "live").ToList();
        var overall = live.Any(c => c.Key == "database" && c.Status == "down")
            ? "down"
            : live.Any(c => c.Status == "down")
                ? "degraded"
                : "ok";

        return new EnvironmentStatusResponse(
            overall,
            DateTimeOffset.UtcNow,
            "Live checks run from this web app against what it can actually reach. Items marked Configured only report App Settings, not a live ping. Connection strings and passwords are never returned.",
            checks);
    }

    private EnvironmentCheck ApiCheck()
    {
        var uptime = DateTimeOffset.UtcNow - _processStarted;
        var minutes = Math.Max(0, (int)uptime.TotalMinutes);
        var hours = minutes / 60;
        var remain = minutes % 60;
        var up = hours > 0 ? $"{hours}h {remain}m" : $"{remain}m";
        return Ok(
            "api",
            "Web app / API",
            "live",
            $"Process is up ({up}). /api/health is served by this process. Environment {_environment.EnvironmentName}.");
    }

    private async Task<EnvironmentCheck> DatabaseCheckAsync(CancellationToken cancellationToken)
    {
        var provider = DescribeDatabase();
        try
        {
            if (!await _db.Database.CanConnectAsync(cancellationToken))
            {
                return Down("database", "SQL Server / database", "live", provider, "The database did not accept a connection.");
            }

            var connection = _db.Database.GetDbConnection();
            var openedHere = connection.State != ConnectionState.Open;
            if (openedHere)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                var scalar = await command.ExecuteScalarAsync(cancellationToken);
                if (scalar is null || Convert.ToInt32(scalar) != 1)
                {
                    return Down("database", "SQL Server / database", "live", provider, "SELECT 1 did not return 1.");
                }
            }
            finally
            {
                if (openedHere)
                {
                    await connection.CloseAsync();
                }
            }

            return Ok("database", "SQL Server / database", "live", $"{provider} Connected. SELECT 1 succeeded.");
        }
        catch (Exception ex)
        {
            return Down("database", "SQL Server / database", "live", provider, Sanitize(ex.Message));
        }
    }

    private async Task<EnvironmentCheck> StorageCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            var probe = await _storage.ProbeAsync(cancellationToken);
            return probe.Ok
                ? Ok("storage", "Blob / storage", "live", $"{probe.Provider}. {probe.Detail}")
                : Down("storage", "Blob / storage", "live", $"{probe.Provider}. {probe.Detail}", Sanitize(probe.Error));
        }
        catch (Exception ex)
        {
            return Down("storage", "Blob / storage", "live", "Work file storage.", Sanitize(ex.Message));
        }
    }

    private async Task<EnvironmentCheck> KeyVaultCheckAsync(CancellationToken cancellationToken)
    {
        var raw = _configuration["KeyVaultUri"] ?? _configuration["KeyVault:VaultUri"];
        if (string.IsNullOrWhiteSpace(raw) || !Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            return new EnvironmentCheck(
                "keyVault",
                "Key Vault",
                "not_configured",
                "configured",
                "KeyVaultUri is not set. Live secret read is skipped.",
                null);
        }

        var host = uri.Host;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(12));
            var client = new SecretClient(uri, new DefaultAzureCredential());
            await foreach (var _ in client.GetPropertiesOfSecretsAsync().WithCancellation(timeout.Token))
            {
                return Ok("keyVault", "Key Vault", "live", $"{host} is reachable. Secret names were listed; values are not shown.");
            }

            return Ok("keyVault", "Key Vault", "live", $"{host} is reachable. No secrets were listed.");
        }
        catch (Exception ex)
        {
            return Down("keyVault", "Key Vault", "live", $"{host} is configured.", Sanitize(ex.Message));
        }
    }

    private EnvironmentCheck AppInsightsCheck()
    {
        var configured = !string.IsNullOrWhiteSpace(_configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"])
            || !string.IsNullOrWhiteSpace(_configuration["ApplicationInsights:ConnectionString"]);
        if (!configured)
        {
            return new EnvironmentCheck(
                "appInsights",
                "Application Insights",
                "not_configured",
                "configured",
                "No Application Insights connection string is set. This is a configuration check, not a live ingest ping.",
                null);
        }

        return new EnvironmentCheck(
            "appInsights",
            "Application Insights",
            "ok",
            "configured",
            "Connection string is present. This is a configuration check, not a live ingest ping.",
            null);
    }

    private EnvironmentCheck EmailCheck()
    {
        var settings = _email.Current;
        if (!settings.IsConfigured)
        {
            return new EnvironmentCheck(
                "email",
                "Email / SMTP",
                "not_configured",
                "configured",
                "Email / SMTP is not configured. Set it under Admin Settings, or Email__Enabled and Email__Smtp__Host. No test message was sent.",
                null);
        }

        return new EnvironmentCheck(
            "email",
            "Email / SMTP",
            "ok",
            "configured",
            "SMTP is configured. Send a test email from Admin Settings to verify delivery. No test message was sent here.",
            null);
    }

    private EnvironmentCheck AzureOpenAiCheck()
    {
        if (!_openAi.IsConfigured)
        {
            return new EnvironmentCheck(
                "azureOpenAI",
                "Azure OpenAI",
                "not_configured",
                "configured",
                "AzureOpenAI__Endpoint and AzureOpenAI__ApiKey are not set. AI fill from PDF fails closed. Prefer resource oai-bis-deed-ai, deployment gpt-4.1-mini. This is a configuration check, not a live chat ping.",
                null);
        }

        var host = Uri.TryCreate(_openAi.Endpoint, UriKind.Absolute, out var uri) ? uri.Host : "endpoint set";
        return new EnvironmentCheck(
            "azureOpenAI",
            "Azure OpenAI",
            "ok",
            "configured",
            $"{host} · deployment {_openAi.EffectiveDeployment}. Endpoint and key are present. This is a configuration check, not a live chat ping.",
            null);
    }

    private string DescribeDatabase()
    {
        var raw = _configuration.GetConnectionString("DefaultConnection")
            ?? _configuration["SqlConnectionString"]
            ?? "";
        if (DependencyInjection.IsSqlite(raw))
        {
            return "SQLite (local).";
        }

        if (raw.Contains("database.windows.net", StringComparison.OrdinalIgnoreCase))
        {
            return "Azure SQL.";
        }

        return "SQL Server.";
    }

    private static EnvironmentCheck Ok(string key, string name, string mode, string detail) =>
        new(key, name, "ok", mode, detail, null);

    private static EnvironmentCheck Down(string key, string name, string mode, string detail, string? error) =>
        new(key, name, "down", mode, detail, string.IsNullOrWhiteSpace(error) ? "Check failed." : error);

    private static string Sanitize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Check failed.";
        }

        var cleaned = Regex.Replace(
            message,
            @"(Password|Pwd|AccountKey|SharedAccessSignature|ApiKey|Secret|User ID|User Id|UID)=[^;\s]+",
            "$1=***",
            RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"[A-Za-z0-9+/=]{40,}", "***");
        return cleaned.Length > 240 ? cleaned[..240] + "…" : cleaned;
    }

    private static DateTimeOffset ProcessStartUtc()
    {
        try
        {
            return Process.GetCurrentProcess().StartTime.ToUniversalTime();
        }
        catch
        {
            return DateTimeOffset.UtcNow;
        }
    }
}

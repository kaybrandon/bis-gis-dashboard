using System.Security.Cryptography;
using System.Text;
using GisDashboard.Application.Auth;
using GisDashboard.Application.Email;
using GisDashboard.Application.Exceptions;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Email;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GisDashboard.Infrastructure.Services;

public sealed class PasswordResetService : IPasswordResetService
{
    public const string UnavailableMessage = "Password reset isn’t available yet — contact your admin.";
    public const string GenericMessage = "If an account exists, we sent a reset link.";
    public const string InvalidLinkMessage = "This reset link is invalid or has expired.";

    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(2);

    private readonly UserManager<ApplicationUser> _users;
    private readonly AppDbContext _db;
    private readonly IEmailSender _email;
    private readonly IEmailSettingsCache _cache;
    private readonly EmailOptions _emailOptions;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        UserManager<ApplicationUser> users,
        AppDbContext db,
        IEmailSender email,
        IEmailSettingsCache cache,
        IOptions<EmailOptions> emailOptions,
        IHttpContextAccessor http,
        ILogger<PasswordResetService> logger)
    {
        _users = users;
        _db = db;
        _email = email;
        _cache = cache;
        _emailOptions = emailOptions.Value;
        _http = http;
        _logger = logger;
    }

    public Task<PasswordResetStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var available = _cache.Current.IsConfigured;
        return Task.FromResult(new PasswordResetStatus(
            available,
            available ? GenericMessage : UnavailableMessage));
    }

    public async Task<ForgotPasswordResult> RequestAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (!_cache.Current.IsConfigured)
        {
            throw new ServiceUnavailableException(UnavailableMessage);
        }

        var identifier = FirstNonEmpty(request.EmailOrUsername, request.Email, request.UserName);
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ValidationException("Email or username is required.");
        }

        identifier = identifier.Trim();
        var user = await FindActiveUserAsync(identifier);
        if (user is not null)
        {
            await SendResetLinkAsync(user, cancellationToken);
        }
        else
        {
            _logger.LogInformation("Password reset requested for an unknown or inactive sign-in.");
        }

        return new ForgotPasswordResult(GenericMessage);
    }

    public async Task<ResetPasswordResult> ResetAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var token = (request.Token ?? string.Empty).Trim();
        var password = request.NewPassword ?? string.Empty;
        var confirm = request.ConfirmPassword ?? string.Empty;

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ValidationException(InvalidLinkMessage);
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ValidationException("New password is required.");
        }

        if (password != confirm)
        {
            throw new ValidationException("New password and confirmation do not match.");
        }

        var hash = HashToken(token);
        var now = DateTimeOffset.UtcNow;
        var row = await _db.PasswordResetTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == hash && x.UsedAt == null, cancellationToken);

        if (row is null || row.ExpiresAt <= now || !row.User.IsActive)
        {
            throw new ValidationException(InvalidLinkMessage);
        }

        var identityToken = await _users.GeneratePasswordResetTokenAsync(row.User);
        var reset = await _users.ResetPasswordAsync(row.User, identityToken, password);
        if (!reset.Succeeded)
        {
            throw new ValidationException(string.Join(" ", reset.Errors.Select(e => e.Description)));
        }

        row.UsedAt = now;
        var leftovers = await _db.PasswordResetTokens
            .Where(x => x.UserId == row.UserId && x.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var extra in leftovers)
        {
            extra.UsedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new ResetPasswordResult("Your password was reset. You can sign in now.");
    }

    private async Task SendResetLinkAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var pending = await _db.PasswordResetTokens
            .Where(x => x.UserId == user.Id && x.UsedAt == null)
            .ToListAsync(cancellationToken);
        if (pending.Any(x => x.CreatedAt > now - ResendCooldown))
        {
            return;
        }

        foreach (var old in pending)
        {
            old.UsedAt = now;
        }

        var raw = CreateToken();
        _db.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(raw),
            CreatedAt = now,
            ExpiresAt = now.Add(TokenLifetime)
        });
        await _db.SaveChangesAsync(cancellationToken);

        var link = $"{PublicOrigin()}/reset-password?token={Uri.EscapeDataString(raw)}";
        var result = await _email.SendAsync(
            new OutboundEmail(
                [user.Email ?? string.Empty],
                "Reset your GIS Dashboard password",
                $"""
                <p>We received a request to reset the GIS Dashboard password for this email.</p>
                <p><a href="{System.Net.WebUtility.HtmlEncode(link)}">Choose a new password</a>. This link expires in 30 minutes.</p>
                <p>If you did not ask for this, you can ignore the message.</p>
                """,
                $"Reset your GIS Dashboard password (expires in 30 minutes):\n{link}\n\nIf you did not ask for this, you can ignore the message.",
                []),
            cancellationToken);

        if (!result.Delivered)
        {
            _logger.LogWarning("Password reset email was not delivered. Mode={Mode}", result.Mode);
        }
    }

    private async Task<ApplicationUser?> FindActiveUserAsync(string identifier)
    {
        var user = await _users.FindByEmailAsync(identifier)
            ?? await _users.FindByNameAsync(identifier);
        return user is { IsActive: true } ? user : null;
    }

    private string PublicOrigin()
    {
        if (!string.IsNullOrWhiteSpace(_emailOptions.PublicBaseUrl))
        {
            return _emailOptions.PublicBaseUrl.TrimEnd('/');
        }

        var request = _http.HttpContext?.Request;
        if (request is not null && !string.IsNullOrWhiteSpace(request.Host.Value))
        {
            return $"{request.Scheme}://{request.Host.Value}".TrimEnd('/');
        }

        return "http://127.0.0.1:47222";
    }

    private static string CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}

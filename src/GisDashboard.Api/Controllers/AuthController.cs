using GisDashboard.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GisDashboard.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IPasswordResetService _passwords;

    public AuthController(IAuthService auth, IPasswordResetService passwords)
    {
        _auth = auth;
        _passwords = passwords;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public Task<LoginResponse> Login([FromBody] LoginRequest request, CancellationToken cancellationToken) =>
        _auth.LoginAsync(request, cancellationToken);

    [AllowAnonymous]
    [HttpGet("password-reset")]
    public Task<PasswordResetStatus> PasswordResetStatus(CancellationToken cancellationToken) =>
        _passwords.GetStatusAsync(cancellationToken);

    [AllowAnonymous]
    [EnableRateLimiting("password-reset")]
    [HttpPost("forgot-password")]
    public Task<ForgotPasswordResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken) =>
        _passwords.RequestAsync(request, cancellationToken);

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public Task<ResetPasswordResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken) =>
        _passwords.ResetAsync(request, cancellationToken);

    [Authorize]
    [HttpGet("me")]
    public Task<AuthUser> Me(CancellationToken cancellationToken) =>
        _auth.GetCurrentAsync(cancellationToken);

    [Authorize]
    [HttpPut("me")]
    public Task<AuthUser> UpdateMe([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken) =>
        _auth.UpdateProfileAsync(request, cancellationToken);

    [Authorize]
    [HttpPost("me/avatar")]
    [RequestSizeLimit(2_097_152)]
    public async Task<AuthUser> UploadAvatar(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new Application.Exceptions.ValidationException("A photo is required.");
        }

        await using var stream = file.OpenReadStream();
        return await _auth.UploadAvatarAsync(stream, file.FileName, file.ContentType, file.Length, cancellationToken);
    }

    [Authorize]
    [HttpGet("me/avatar")]
    public async Task<IActionResult> Avatar(CancellationToken cancellationToken)
    {
        var (stream, contentType) = await _auth.GetAvatarAsync(cancellationToken);
        return File(stream, contentType);
    }
}

namespace GisDashboard.Application.Auth;

public sealed class ForgotPasswordRequest
{
    public string? Email { get; set; }
    public string? UserName { get; set; }
    public string? EmailOrUsername { get; set; }
}

public sealed class ResetPasswordRequest
{
    public string? Token { get; set; }
    public string? NewPassword { get; set; }
    public string? ConfirmPassword { get; set; }
}

public sealed record PasswordResetStatus(bool Available, string Message);

public sealed record ForgotPasswordResult(string Message);

public sealed record ResetPasswordResult(string Message);

public interface IPasswordResetService
{
    Task<PasswordResetStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<ForgotPasswordResult> RequestAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
    Task<ResetPasswordResult> ResetAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
}

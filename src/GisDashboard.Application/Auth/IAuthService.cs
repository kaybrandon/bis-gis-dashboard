namespace GisDashboard.Application.Auth;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthUser> GetCurrentAsync(CancellationToken cancellationToken = default);
    Task<AuthUser> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default);
    Task<AuthUser> UploadAvatarAsync(Stream content, string fileName, string contentType, long contentLength, CancellationToken cancellationToken = default);
    Task<(Stream Stream, string ContentType)> GetAvatarAsync(CancellationToken cancellationToken = default);
}

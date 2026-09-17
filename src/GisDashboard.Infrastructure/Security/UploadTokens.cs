using System.Security.Cryptography;

namespace GisDashboard.Infrastructure.Security;

public static class UploadTokens
{
    public static string Create()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public static bool IsValidFormat(string? token) =>
        !string.IsNullOrWhiteSpace(token) && token.Trim().Length >= 16;
}

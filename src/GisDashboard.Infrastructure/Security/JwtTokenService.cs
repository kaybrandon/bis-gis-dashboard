using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GisDashboard.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GisDashboard.Infrastructure.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "gis-dashboard";
    public string Audience { get; set; } = "gis-dashboard";
    public string Key { get; set; } = string.Empty;
    public int ExpiresMinutes { get; set; } = 480;
}

public sealed class JwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.Key) || _options.Key.Length < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be at least 32 characters. Set it from Key Vault in Azure.");
        }
    }

    public (string Token, DateTimeOffset ExpiresAt) Create(ApplicationUser user, string role)
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(_options.ExpiresMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.PublicName),
            new(ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace SadcOMS.API.Auth;

public sealed class DevJwtTokenService
{
    private readonly AuthOptions _options;

    public DevJwtTokenService(IOptions<AuthOptions> options) => _options = options.Value;

    public (string AccessToken, int ExpiresInSeconds) CreateAccessToken(TimeSpan? lifetime = null)
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey) || _options.SigningKey.Length < 32)
            throw new InvalidOperationException("Auth:SigningKey must be at least 32 characters for dev JWT minting.");

        var expiresIn = lifetime ?? TimeSpan.FromHours(1);
        var expiresAt = DateTime.UtcNow.Add(expiresIn);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, "dev-user"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        foreach (var scope in new[] { "orders.read", "orders.write", "customers.write" })
            claims.Add(new Claim("scp", scope));
        claims.Add(new Claim(ClaimTypes.Role, "SadcOMS.Admin"));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        return (accessToken, (int)expiresIn.TotalSeconds);
    }
}

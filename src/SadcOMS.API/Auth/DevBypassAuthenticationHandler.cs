using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace SadcOMS.API.Auth;

/// <summary>
/// Intentional demo mock: authenticates every request as a fixed dev principal when
/// <see cref="AuthOptions.DevBypass"/> is enabled. Disabled in production configuration.
/// </summary>
public sealed class DevBypassAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public DevBypassAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "dev-user"),
            new("sub", "dev-user"),
            new(ClaimTypes.Role, "SadcOMS.Admin"),
        };

        foreach (var scope in new[] { "orders.read", "orders.write", "customers.write" })
            claims.Add(new Claim("scp", scope));

        var identity = new ClaimsIdentity(claims, AuthConstants.DevBypassScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthConstants.DevBypassScheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

namespace SadcOMS.API.Auth;

public class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// When true, every request is authenticated as a fixed dev principal (demo/reviewer mode).
    /// Must be false in production.
    /// </summary>
    public bool DevBypass { get; set; }

    /// <summary>
    /// Microsoft Entra authority, e.g. https://login.microsoftonline.com/{tenantId}/v2.0 .
    /// When set, JWT validation uses OIDC metadata instead of <see cref="SigningKey"/>.
    /// </summary>
    public string? Authority { get; set; }

    public string Audience { get; set; } = "sadcoms-api";

    public string Issuer { get; set; } = "sadcoms-dev";

    /// <summary>Symmetric signing key for local/dev JWT minting and validation (min 32 chars).</summary>
    public string SigningKey { get; set; } = string.Empty;
}

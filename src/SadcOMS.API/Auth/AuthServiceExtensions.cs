using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace SadcOMS.API.Auth;

public static class AuthServiceExtensions
{
    public static IServiceCollection AddSadcOmsAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.AddSingleton<DevJwtTokenService>();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = AuthConstants.SmartScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddPolicyScheme(AuthConstants.SmartScheme, "Authentication router", options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                var authOptions = context.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value;
                return authOptions.DevBypass
                    ? AuthConstants.DevBypassScheme
                    : JwtBearerDefaults.AuthenticationScheme;
            };
        })
        .AddScheme<AuthenticationSchemeOptions, DevBypassAuthenticationHandler>(
            AuthConstants.DevBypassScheme,
            _ => { })
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var authOptions = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>()
                ?? new AuthOptions();

            ConfigureJwtBearer(options, authOptions);
        });

        services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, JwtBearerOptionsPostConfigure>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy("OrdersWrite", policy =>
                policy.RequireClaim("scp", "orders.write"));
            options.AddPolicy("OrdersRead", policy =>
                policy.RequireClaim("scp", "orders.read"));
            options.AddPolicy("CustomersWrite", policy =>
                policy.RequireClaim("scp", "customers.write"));
            options.AddPolicy("Admin", policy =>
                policy.RequireRole("SadcOMS.Admin"));
        });

        return services;
    }

    internal static void ConfigureJwtBearer(JwtBearerOptions options, AuthOptions authOptions)
    {
        // Swap Auth:Authority + Auth:Audience for Microsoft Entra; keep symmetric SigningKey for local dev.
        if (!string.IsNullOrWhiteSpace(authOptions.Authority))
        {
            options.Authority = authOptions.Authority;
            options.Audience = authOptions.Audience;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
            };
        }
        else
        {
            if (string.IsNullOrWhiteSpace(authOptions.SigningKey) || authOptions.SigningKey.Length < 32)
            {
                throw new InvalidOperationException(
                    "Auth:SigningKey must be at least 32 characters when Auth:Authority is not configured.");
            }

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.SigningKey));
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = authOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = authOptions.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
                ClockSkew = TimeSpan.FromMinutes(1),
            };
        }
    }

    private sealed class JwtBearerOptionsPostConfigure : IPostConfigureOptions<JwtBearerOptions>
    {
        private readonly IOptionsMonitor<AuthOptions> _authOptions;

        public JwtBearerOptionsPostConfigure(IOptionsMonitor<AuthOptions> authOptions) =>
            _authOptions = authOptions;

        public void PostConfigure(string? name, JwtBearerOptions options)
        {
            if (!string.Equals(name, JwtBearerDefaults.AuthenticationScheme, StringComparison.Ordinal))
                return;

            ConfigureJwtBearer(options, _authOptions.CurrentValue);
        }
    }
}

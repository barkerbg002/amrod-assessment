using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SadcOMS.API.DTOs;
using SadcOMS.Infrastructure.Persistence;

namespace SadcOMS.Tests.Integration;

public class AuthIntegrationTests
{
    [Fact]
    public async Task CreateCustomer_DevBypassOn_NoToken_ReturnsCreated()
    {
        using var factory = CreateFactory(devBypass: true);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/customers", new CreateCustomerRequest(
            "Bypass Customer", $"bypass{Guid.NewGuid():N}@example.com", "ZA"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateCustomer_DevBypassOff_NoToken_ReturnsUnauthorized()
    {
        using var factory = CreateFactory(devBypass: false);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/customers", new CreateCustomerRequest(
            "No Auth Customer", $"noauth{Guid.NewGuid():N}@example.com", "ZA"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateCustomer_DevBypassOff_ValidToken_ReturnsCreated()
    {
        using var tokenFactory = CreateFactory(devBypass: true);
        var token = await MintDevTokenAsync(tokenFactory);

        using var factory = CreateFactory(devBypass: false);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/customers", new CreateCustomerRequest(
            "Token Customer", $"token{Guid.NewGuid():N}@example.com", "ZA"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(bool devBypass)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Development);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:DevBypass"] = devBypass ? "true" : "false",
                    ["Auth:Issuer"] = "sadcoms-dev",
                    ["Auth:Audience"] = "sadcoms-api",
                    ["Auth:SigningKey"] = SadcOmsWebApplicationFactory.TestSigningKey,
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(DbContextOptions<SadcOmsDbContext>));
                services.AddDbContext<SadcOmsDbContext>(options =>
                    options.UseInMemoryDatabase($"SadcOmsAuth_{devBypass}_{Guid.NewGuid():N}"));
            });
        });
    }

    private static async Task<string> MintDevTokenAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsync("/api/auth/token", content: null);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions());
        return json!.AccessToken;
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}

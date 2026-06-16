using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SadcOMS.Infrastructure.Persistence;

namespace SadcOMS.Tests.Integration;

public class SadcOmsWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestSigningKey = "sadcoms-dev-signing-key-min-32-chars!!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:DevBypass"] = "true",
                ["Auth:Issuer"] = "sadcoms-dev",
                ["Auth:Audience"] = "sadcoms-api",
                ["Auth:SigningKey"] = TestSigningKey,
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<SadcOmsDbContext>));
            services.AddDbContext<SadcOmsDbContext>(options =>
                options.UseInMemoryDatabase("SadcOmsIntegrationTests"));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<SadcOmsDbContext>().Database.EnsureCreated();
        return host;
    }

    /// <summary>
    /// InMemory EF does not auto-increment SQL Server rowversion tokens; simulate another writer.
    /// </summary>
    public async Task SimulateRowVersionBumpAsync(Guid orderId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SadcOmsDbContext>();
        var order = await db.Orders.FindAsync(orderId)
            ?? throw new InvalidOperationException($"Order '{orderId}' not found.");
        order.RowVersion = Guid.NewGuid().ToByteArray();
        await db.SaveChangesAsync();
    }
}

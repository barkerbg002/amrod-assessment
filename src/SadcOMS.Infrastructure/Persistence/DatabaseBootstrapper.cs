using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SadcOMS.Infrastructure.Persistence;

public static class DatabaseBootstrapper
{
    public static void ApplyMigrations(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SadcOmsDbContext>();
        var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SadcOmsDbContext>>();

        if (!db.Database.IsRelational())
        {
            logger.LogInformation("Non-relational database provider detected; ensuring schema is created.");
            db.Database.EnsureCreated();
            return;
        }

        var pending = db.Database.GetPendingMigrations().ToList();
        if (pending.Count > 0)
        {
            logger.LogInformation("Applying {Count} pending migration(s): {Migrations}",
                pending.Count, string.Join(", ", pending));
            db.Database.Migrate();
        }

        if (SchemaExists(db))
        {
            logger.LogInformation("Database schema is ready.");
            return;
        }

        logger.LogWarning("Schema tables are missing after migration check.");

        if (!env.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Database schema is not initialized. Run EF Core migrations before starting the API.");
        }

        logger.LogWarning("Development mode: recreating database schema with EnsureCreated().");
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        logger.LogInformation("Database schema created successfully.");
    }

    private static bool SchemaExists(SadcOmsDbContext db)
    {
        try
        {
            db.Database.ExecuteSqlRaw("SELECT TOP 1 [Id] FROM [Customers]");
            return true;
        }
        catch
        {
            return false;
        }
    }
}

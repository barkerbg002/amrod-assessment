using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SadcOMS.Infrastructure.Persistence;

public class SadcOmsDbContextFactory : IDesignTimeDbContextFactory<SadcOmsDbContext>
{
    public SadcOmsDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../SadcOMS.API"))
            .AddJsonFile("appsettings.json")
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=localhost,1433;Database=SadcOMS;User Id=sa;Password=Your_strong_Password123;TrustServerCertificate=True;";

        var optionsBuilder = new DbContextOptionsBuilder<SadcOmsDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new SadcOmsDbContext(optionsBuilder.Options);
    }
}

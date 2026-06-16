using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SadcOMS.Infrastructure.Fx;
using SadcOMS.Infrastructure.Messaging;
using SadcOMS.Infrastructure.Persistence;
using SadcOMS.Infrastructure.Repositories;

namespace SadcOMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString)
    {
        services.AddDbContext<SadcOmsDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddMemoryCache();
        services.Configure<FxCacheOptions>(configuration.GetSection(FxCacheOptions.SectionName));
        services.AddSingleton<IFxRateProvider, MockFxRateProvider>();

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();

        return services;
    }

    public static IServiceCollection AddRabbitMqPublisher(this IServiceCollection services)
    {
        services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
        return services;
    }
}

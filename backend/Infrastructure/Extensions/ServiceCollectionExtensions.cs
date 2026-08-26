using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Core.Interfaces;
using RealTimeEventAnalyticsEngine.Core.Queries;
using RealTimeEventAnalyticsEngine.Core.Validation;
using RealTimeEventAnalyticsEngine.Infrastructure.Cache;
using RealTimeEventAnalyticsEngine.Infrastructure.Data;
using StackExchange.Redis;

namespace RealTimeEventAnalyticsEngine.Infrastructure.Extensions;

/// <summary>
/// Extension methods that register all infrastructure and application services.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCacheServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var connectionString = configuration.GetValue<string>("Redis:ConnectionString") ?? "localhost:6379";

            var options = ConfigurationOptions.Parse(connectionString);
            options.AbortOnConnectFail = false;
            options.ConnectRetry = 3;
            options.ConnectTimeout = 3000;
            options.SyncTimeout = 3000;

            return ConnectionMultiplexer.Connect(options);
        });

        services.AddSingleton<IRedisCacheService, RedisCacheService>();

        return services;
    }

    public static IServiceCollection AddDataServices(this IServiceCollection services)
    {
        services.AddSingleton<IEventWriteQueue, EventWriteQueue>();
        services.AddSingleton<IEventAnalyticsDbContext, EventAnalyticsDbContext>();
        services.AddSingleton<IEventRepository, EventWriteRepository>();

        // Concrete type is also registered because DatabaseInitializer depends on it directly
        services.AddSingleton<EventWriteRepository>();
        services.AddSingleton<DatabaseInitializer>();

        services.AddHostedService<EventWriteBackgroundService>();

        return services;
    }

    public static IServiceCollection AddApplicationHandlers(this IServiceCollection services)
    {
        services.AddTransient<LogEventCommandHandler>();
        services.AddTransient<FetchDashboardDataQueryHandler>();

        return services;
    }

    public static IServiceCollection AddValidationServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<LogEventCommandValidator>(ServiceLifetime.Singleton);
        return services;
    }
}
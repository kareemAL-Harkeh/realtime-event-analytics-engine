using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Core.Queries;
using RealTimeEventAnalyticsEngine.Core.Validation;
using RealTimeEventAnalyticsEngine.Infrastructure.Cache;
using RealTimeEventAnalyticsEngine.Infrastructure.Data;
using RealTimeEventAnalyticsEngine.Core.Interfaces;

namespace RealTimeEventAnalyticsEngine.Infrastructure.Extensions;

/// <summary>
/// Consolidated modern service descriptor catalog for high-throughput pipeline registration.
/// Implements loose-coupling and thread-safe dependency lifetimes under .NET 10.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers operational StackExchange Redis multi-plexed memory pools.</summary>
    public static IServiceCollection AddCacheServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var connStr = configuration.GetValue<string>("Redis:ConnectionString") ?? "localhost:6379";
            var options = ConfigurationOptions.Parse(connStr);
            options.AbortOnConnectFail = false;
            options.ConnectRetry = 3;
            options.ConnectTimeout = 2000;
            options.SyncTimeout = 2000;
            return ConnectionMultiplexer.Connect(options);
        });

        services.AddSingleton<IRedisCacheService, RedisCacheService>();
        return services;
    }

    /// <summary>Registers reliable PostgreSQL persistence drivers, atomic channels and worker pools.</summary>
    public static IServiceCollection AddDataServices(this IServiceCollection services)
    {
        // Thread-safe singleton channels and database contexts
        services.AddSingleton<IEventWriteQueue, EventWriteQueue>();
        services.AddSingleton<IEventRepository, EventWriteRepository>();
        services.AddSingleton<IEventAnalyticsDbContext, EventAnalyticsDbContext>();
        services.AddSingleton<EventWriteRepository>();
        services.AddSingleton<DatabaseInitializer>();
        
        // Non-blocking long-running background pipeline consumer
        services.AddHostedService<EventWriteBackgroundService>();
        return services;
    }

    /// <summary>Registers CQRS processors using clean request isolation scopes.</summary>
    public static IServiceCollection AddApplicationHandlers(this IServiceCollection services)
    {
        // Converted to Transient to safely allow handlers to consume scoped assets without locking
        services.AddTransient<LogEventCommandHandler>();
        services.AddTransient<FetchDashboardDataQueryHandler>();
        return services;
    }

    /// <summary>Compiles and registers domain logic FluentValidators into memory.</summary>
    public static IServiceCollection AddValidationServices(this IServiceCollection services)
    {
        // Avoid legacy reflective AddFluentValidationAutoValidation for optimal performance.
        // Handlers or Endpoints can now explicitly inject and use IValidator<T> blazing fast.
        services.AddValidatorsFromAssemblyContaining<LogEventCommandValidator>(ServiceLifetime.Singleton);
        return services;
    }
}
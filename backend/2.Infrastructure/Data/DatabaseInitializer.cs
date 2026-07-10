using System.Data.Common;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace RealTimeEventAnalyticsEngine.Infrastructure.Data;

/// <summary>
/// Handles server startup migrations, raw database bootstrapping, and analytics test data seeding.
/// </summary>
public sealed class DatabaseInitializer
{
    private readonly IConfiguration _configuration;
    private readonly EventWriteRepository _eventRepository;
    private readonly ILogger<DatabaseInitializer> _logger;
    private readonly string _eventStoreConnectionString;

    public DatabaseInitializer(IConfiguration configuration, EventWriteRepository eventRepository, ILogger<DatabaseInitializer> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _eventStoreConnectionString = _configuration.GetConnectionString("EventStore")
            ?? throw new InvalidOperationException("Critical configuration missing: 'EventStore' connection string is null.");
    }

    /// <summary>
    /// Coordinates database existence verification, table schema creation, and lookup data provisioning.
    /// </summary>
    public async Task EnsureDatabaseAndSeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(_eventStoreConnectionString);

            if (string.IsNullOrWhiteSpace(builder.Database))
            {
                throw new InvalidOperationException("The 'EventStore' connection string must declare a targeted database name.");
            }

            // 1. Ensure Postgres instance has the logical database initialized
            await EnsureDatabaseExistsAsync(builder, cancellationToken);

            // 2. Build out raw relational schema
            await _eventRepository.EnsureEventTableExistsAsync(cancellationToken);

            // 3. Populate test telemetries if system is fresh
            if (!await _eventRepository.HasAnyEventsAsync(cancellationToken))
            {
                await _eventRepository.SeedSampleEventsAsync(cancellationToken);
            }
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException or TimeoutException)
        {
            _logger.LogWarning(ex, "Database is not available at startup. The backend will continue without initializing data.");
        }
    }

    private static async Task EnsureDatabaseExistsAsync(NpgsqlConnectionStringBuilder eventStoreStringBuilder, CancellationToken cancellationToken)
    {
        var adminBuilder = new NpgsqlConnectionStringBuilder(eventStoreStringBuilder.ConnectionString)
        {
            Database = "postgres" // Connect to standard root admin db to check existence
        };

        await using var adminConnection = new NpgsqlConnection(adminBuilder.ConnectionString);
        await adminConnection.OpenAsync(cancellationToken);

        var targetDbName = eventStoreStringBuilder.Database!;
        
        var checkDbSql = "SELECT EXISTS(SELECT 1 FROM pg_database WHERE datname = @DatabaseName);";
        var exists = await adminConnection.ExecuteScalarAsync<bool>(new CommandDefinition(checkDbSql, new { DatabaseName = targetDbName }, cancellationToken: cancellationToken));

        if (!exists)
        {
            // Sanitize and safely construct database instantiation statement
            var createDbSql = $"CREATE DATABASE \"{targetDbName}\";";
            await adminConnection.ExecuteAsync(new CommandDefinition(createDbSql, cancellationToken: cancellationToken));
        }
    }
}
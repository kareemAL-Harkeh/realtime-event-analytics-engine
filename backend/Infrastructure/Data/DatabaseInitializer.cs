using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Dapper;

namespace RealTimeEventAnalyticsEngine.Infrastructure.Data;

/// <summary>
/// Runs once at startup to make sure the database and table exist,
/// and optionally seeds sample data when the table is empty (Development only).
/// </summary>
public sealed class DatabaseInitializer
{
    private readonly IConfiguration _configuration;
    private readonly EventWriteRepository _repository;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DatabaseInitializer> _logger;
    private readonly string _connectionString;

    public DatabaseInitializer(
        IConfiguration configuration,
        EventWriteRepository repository,
        IHostEnvironment environment,
        ILogger<DatabaseInitializer> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _connectionString = _configuration.GetConnectionString("EventStore")
            ?? throw new InvalidOperationException(
                "Connection string 'EventStore' is missing from configuration.");
    }

    public async Task EnsureDatabaseAndSeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(_connectionString);

            if (string.IsNullOrWhiteSpace(builder.Database))
            {
                throw new InvalidOperationException(
                    "The 'EventStore' connection string must contain a database name.");
            }

            // 1. Make sure the database itself exists
            await EnsureDatabaseExistsAsync(builder, cancellationToken);

            // 2. Make sure the events table + indexes exist. This is schema
            //    bootstrapping and is safe to run in every environment.
            await _repository.EnsureEventTableExistsAsync(cancellationToken);

            // 3. Sample/fake data (Bogus-generated) is a local development
            //    convenience ONLY. If this ran unconditionally, the very first
            //    time a Staging or Production table happened to be empty -
            //    first deploy, post-incident recovery, a bad migration - the
            //    system would silently populate it with 1000 fabricated events
            //    that look exactly like real telemetry. There would be no error,
            //    no warning in the UI, just wrong data quietly feeding the
            //    dashboard. Seeding non-development environments has to be an
            //    explicit, deliberate action (a migration/admin script), never
            //    something that "just happens" because a table was empty.
            if (!_environment.IsDevelopment())
            {
                _logger.LogInformation(
                    "Skipping sample data seeding - environment is '{Environment}', not Development.",
                    _environment.EnvironmentName);
                return;
            }

            if (!await _repository.HasAnyEventsAsync(cancellationToken))
            {
                _logger.LogInformation("No events found. Seeding sample data (Development only)...");
                await _repository.SeedSampleEventsAsync(cancellationToken);
                _logger.LogInformation("Sample data seeded successfully.");
            }
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException or InvalidOperationException)
        {
            // We deliberately swallow startup DB problems so the API can still start.
            // The background service and endpoints will surface errors later if needed.
            _logger.LogWarning(ex,
                "Database is not available at startup. The application will continue without initialization.");
        }
    }

    private static async Task EnsureDatabaseExistsAsync(
        NpgsqlConnectionStringBuilder eventStoreBuilder,
        CancellationToken cancellationToken)
    {
        var adminBuilder = new NpgsqlConnectionStringBuilder(eventStoreBuilder.ConnectionString)
        {
            Database = "postgres" // connect to the default maintenance database
        };

        await using var adminConnection = new NpgsqlConnection(adminBuilder.ConnectionString);
        await adminConnection.OpenAsync(cancellationToken);

        const string checkSql = "SELECT EXISTS(SELECT 1 FROM pg_database WHERE datname = @DatabaseName);";

        var exists = await adminConnection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                checkSql,
                new { DatabaseName = eventStoreBuilder.Database },
                cancellationToken: cancellationToken));

        if (!exists)
        {
            // Database names cannot be parameterized, so we escape it safely.
            var dbName = eventStoreBuilder.Database!.Replace("\"", "\"\"");
            var createSql = $"CREATE DATABASE \"{dbName}\";";

            await adminConnection.ExecuteAsync(
                new CommandDefinition(createSql, cancellationToken: cancellationToken));
        }
    }
}

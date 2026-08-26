using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace RealTimeEventAnalyticsEngine.Infrastructure.Data;

/// <summary>
/// Concrete PostgreSQL connection factory.
/// </summary>
public sealed class EventAnalyticsDbContext : IEventAnalyticsDbContext
{
    private readonly string _connectionString;

    public EventAnalyticsDbContext(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _connectionString = configuration.GetConnectionString("EventStore")
            ?? throw new InvalidOperationException(
                "Connection string 'EventStore' is missing from configuration.");
    }

    public Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        // We deliberately do not open the connection here.
        // Dapper and the repository code open it when needed.
        DbConnection connection = new NpgsqlConnection(_connectionString);
        return Task.FromResult(connection);
    }
}
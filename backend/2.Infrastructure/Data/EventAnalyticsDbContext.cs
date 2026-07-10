using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace RealTimeEventAnalyticsEngine.Infrastructure.Data;

/// <summary>
/// Concrete infrastructure implementation for PostgreSQL database connections.
/// </summary>
public sealed class EventAnalyticsDbContext : IEventAnalyticsDbContext
{
    private readonly string _connectionString;

    public EventAnalyticsDbContext(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        
        _connectionString = configuration.GetConnectionString("EventStore") 
            ?? throw new InvalidOperationException("Critical configuration missing: 'EventStore' connection string is null.");
    }

    public Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        // Instantiating NpgsqlConnection which natively implements DbConnection and full Async support
        var connection = new NpgsqlConnection(_connectionString);
        return Task.FromResult<DbConnection>(connection);
    }
}
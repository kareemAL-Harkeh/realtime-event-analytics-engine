using System.Data.Common;

namespace RealTimeEventAnalyticsEngine.Infrastructure.Data;

/// <summary>
/// Abstraction that provides asynchronous database connections.
/// Keeps the rest of the infrastructure independent from the concrete ADO.NET provider.
/// </summary>
public interface IEventAnalyticsDbContext
{
    /// <summary>
    /// Creates a new openable DbConnection.
    /// The caller is responsible for disposing the connection.
    /// </summary>
    Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}
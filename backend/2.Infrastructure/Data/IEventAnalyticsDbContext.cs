using System.Data.Common;

namespace RealTimeEventAnalyticsEngine.Infrastructure.Data;

/// <summary>
/// Core database context abstraction providing non-blocking, asynchronous relational connection factory.
/// </summary>
public interface IEventAnalyticsDbContext
{
    /// <summary>
    /// Factory method to spin up a compliant DbConnection tailored for high-performance ADO.NET and Dapper.
    /// </summary>
    Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}
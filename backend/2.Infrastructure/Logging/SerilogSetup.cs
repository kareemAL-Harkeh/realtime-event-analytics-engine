using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace RealTimeEventAnalyticsEngine.Infrastructure.Logging;

/// <summary>
/// Centralized enterprise logging engine configuration powered by Serilog.
/// </summary>
public static class SerilogSetup
{
    public static LoggerConfiguration CreateLoggerConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            // Drop heavy system noise to optimize disk space and CPU
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .WriteTo.Console()
            // Asynchronously log to structured local files without locking API threads
            .WriteTo.Async(writeTo => writeTo.File(
                path: "logs/analytics-runtime-.log",
                rollingInterval: RollingInterval.Day,
                restrictedToMinimumLevel: LogEventLevel.Information,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            ));
    }
}
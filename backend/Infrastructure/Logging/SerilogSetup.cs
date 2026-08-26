using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace RealTimeEventAnalyticsEngine.Infrastructure.Logging;

/// <summary>
/// Builds the Serilog logger configuration used by the application.
/// </summary>
public static class SerilogSetup
{
    public static LoggerConfiguration CreateLoggerConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .WriteTo.Console()
            .WriteTo.Async(writeTo => writeTo.File(
                path: "logs/analytics-runtime-.log",
                rollingInterval: RollingInterval.Day,
                restrictedToMinimumLevel: LogEventLevel.Information,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            ));
    }
}
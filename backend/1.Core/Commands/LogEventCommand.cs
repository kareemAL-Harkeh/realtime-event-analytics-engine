namespace RealTimeEventAnalyticsEngine.Core.Commands;

/// <summary>
/// Immutable high-density telemetry contract capturing structured event logs.
/// </summary>
public sealed record LogEventCommand(
    string EventType,
    string Payload,
    string Source,
    DateTimeOffset Timestamp
);
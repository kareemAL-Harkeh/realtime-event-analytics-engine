namespace RealTimeEventAnalyticsEngine.Core.Commands;

/// <summary>
/// Immutable contract that represents a single telemetry event coming into the system.
/// Designed to be lightweight and safe to pass across threads and queues.
/// </summary>
public sealed record LogEventCommand
{
    /// <summary>
    /// Optional unique identifier for the event. 
    /// Useful for idempotency and distributed tracing. 
    /// If not provided, the system can generate one later.
    /// </summary>
    public string? EventId { get; init; }

    /// <summary>
    /// High-level category of the event (e.g. "user.login", "order.created", "sensor.reading").
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// The actual event data. Usually a JSON string, but can be any serialized payload.
    /// </summary>
    public required string Payload { get; init; }

    /// <summary>
    /// Origin of the event (service name, device id, client app, etc.).
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// When the event originally occurred. 
    /// If not supplied, the handler will automatically set it to UTC now.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }
}
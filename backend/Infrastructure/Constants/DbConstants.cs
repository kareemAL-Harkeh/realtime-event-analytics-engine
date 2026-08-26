namespace RealTimeEventAnalyticsEngine.Infrastructure.Constants;

/// <summary>
/// Database table and column names used across the infrastructure layer.
/// </summary>
public static class DbConstants
{
    public const string EventTable = "event_records";

    public const string EventIdColumn = "id";
    public const string EventTypeColumn = "event_type";
    public const string TimestampColumn = "timestamp";
    public const string PayloadColumn = "payload";
    public const string SourceColumn = "source";
}
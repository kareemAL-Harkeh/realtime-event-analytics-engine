using Microsoft.AspNetCore.SignalR;

namespace RealTimeEventAnalyticsEngine.Presentation.Hubs;

/// <summary>
/// High-frequency SignalR bi-directional transport gateway for live analytical broadcasts.
/// </summary>
public sealed class EventHub : Hub
{
    // Kept lean; clients only connect to listen for "ReceiveEvent"
}
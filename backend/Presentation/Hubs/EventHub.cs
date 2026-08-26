using Microsoft.AspNetCore.SignalR;

namespace RealTimeEventAnalyticsEngine.Presentation.Hubs;

/// <summary>
/// SignalR hub used to push live events to connected dashboard clients.
/// Clients should listen to the "ReceiveEvent" method.
/// </summary>
public sealed class EventHub : Hub
{
    // Intentionally left empty.
    // All broadcasting is done from the background service via IHubContext<EventHub>.
}
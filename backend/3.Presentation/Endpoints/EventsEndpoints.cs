using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Presentation.Responses;

namespace RealTimeEventAnalyticsEngine.Presentation.Endpoints;

/// <summary>
/// High-velocity ingestion endpoints optimized for immediate non-blocking request capturing.
/// </summary>
public static class EventsEndpoints
{
    public static void MapEvents(this WebApplication app)
    {
        app.MapPost("/api/events", async (
            LogEventCommand command,
            LogEventCommandHandler handler,
            IValidator<LogEventCommand> validator,
            ILogger<LogEventCommand> logger) =>
        {
            // 🛠️ Note: We dropped the IHubContext from here to secure absolute sub-millisecond response execution
            logger.LogDebug("Ingesting event command: {EventType} from {Source}", command.EventType, command.Source);
            
            var validation = await validator.ValidateAsync(command);
            if (!validation.IsValid)
            {
                logger.LogWarning("Event validation failed: {Errors}", 
                    string.Join(", ", validation.Errors.Select(e => e.ErrorMessage)));
                    
                return Results.BadRequest(new ApiResponse<object>("ValidationFailed", null!, 
                    string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));
            }

            try
            {
                // 1. Hand off to the fast In-Memory Channel directly via Handler
                await handler.HandleAsync(command);

                // 2. Return 202 Accepted immediately! 
                // The Background worker will handle DB insertion, Redis Caching, and SignalR Live-Broadcasting.
                logger.LogInformation("Event {EventType} securely ingested into background pipeline channels.", command.EventType);
                
                return Results.Accepted("/api/events", new ApiResponse<EventAcceptedResponse>("Success", new EventAcceptedResponse()));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Critical failure registered during event ingestion for type: {EventType}", command.EventType);
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("LogEvent")
        .Produces(StatusCodes.Status202Accepted)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status500InternalServerError);
    }
}
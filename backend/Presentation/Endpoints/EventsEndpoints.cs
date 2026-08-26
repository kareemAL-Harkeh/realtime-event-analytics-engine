using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Presentation.Authentication;
using RealTimeEventAnalyticsEngine.Presentation.Extensions;
using RealTimeEventAnalyticsEngine.Presentation.Responses;

namespace RealTimeEventAnalyticsEngine.Presentation.Endpoints;

/// <summary>
/// Endpoint for high-throughput event ingestion.
/// Returns 202 Accepted as soon as the event is safely queued.
/// </summary>
public static class EventsEndpoints
{
    public static void MapEvents(this WebApplication app)
    {
        app.MapPost("/api/events", async (
            LogEventCommand command,
            LogEventCommandHandler handler,
            IValidator<LogEventCommand> validator,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("EventsEndpoint");

            var validation = await validator.ValidateAsync(command, cancellationToken);
            if (!validation.IsValid)
            {
                var errors = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
                logger.LogWarning("Event validation failed: {Errors}", errors);

                return Results.BadRequest(new ApiResponse<object>(
                    Status: "ValidationFailed",
                    Data: null!,
                    Message: errors));
            }

            // Fast non-blocking enqueue
            var accepted = await handler.HandleAsync(command, cancellationToken);

            if (!accepted)
            {
                // Channel is full → apply back-pressure
                logger.LogWarning("Write queue is full. Rejecting event of type {EventType}", command.EventType);
                return Results.Json(
                    new ApiResponse<object>("QueueFull", null!, "System is under heavy load. Please retry later."),
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            logger.LogDebug("Event {EventType} from {Source} accepted into the write queue", command.EventType, command.Source);

            return Results.Accepted(
                "/api/events",
                new ApiResponse<EventAcceptedResponse>("Success", new EventAcceptedResponse()));
        })
        .WithName("LogEvent")
        .RequireAuthorization(AuthorizationPolicies.IngestionClient)
        .RequireRateLimiting(RateLimitingPolicies.EventsIngestion)
        .Produces<ApiResponse<EventAcceptedResponse>>(StatusCodes.Status202Accepted)
        .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
        .Produces<ApiResponse<object>>(StatusCodes.Status401Unauthorized)
        .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
        .Produces<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)
        .Produces<ApiResponse<object>>(StatusCodes.Status503ServiceUnavailable)
        .Produces(StatusCodes.Status500InternalServerError);
    }
}

using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RealTimeEventAnalyticsEngine.Core.Constants;
using RealTimeEventAnalyticsEngine.Core.Queries;
using RealTimeEventAnalyticsEngine.Presentation.Authentication;
using RealTimeEventAnalyticsEngine.Presentation.Extensions;
using RealTimeEventAnalyticsEngine.Presentation.Responses;

namespace RealTimeEventAnalyticsEngine.Presentation.Endpoints;

/// <summary>
/// Read-only endpoint that returns the current dashboard overview.
/// </summary>
public static class DashboardEndpoints
{
    public static void MapDashboard(this WebApplication app)
    {
        app.MapGet("/api/dashboard", async (
            int? windowMinutes,
            FetchDashboardDataQueryHandler handler,
            IValidator<FetchDashboardDataQuery> validator,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("DashboardEndpoint");

            // Use the provided value or fall back to the shared default (30 days).
            var query = new FetchDashboardDataQuery(
                windowMinutes is > 0 ? windowMinutes.Value : DashboardWindowDefaults.DefaultWindowMinutes);

            var validation = await validator.ValidateAsync(query, cancellationToken);
            if (!validation.IsValid)
            {
                var errors = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
                logger.LogWarning("Dashboard validation failed: {Errors}", errors);

                return Results.BadRequest(new ApiResponse<object>(
                    Status: "ValidationFailed",
                    Data: null!,
                    Message: errors));
            }

            try
            {
                // Propagating the request's own cancellation token means that if the
                // client disconnects mid-request, we stop doing unnecessary work
                // (cache lookups, DB queries) instead of finishing a response nobody
                // will ever read.
                var dashboard = await handler.HandleAsync(query, cancellationToken);

                logger.LogDebug(
                    "Dashboard served. Window={Window}min, TotalEvents={Total}",
                    query.WindowMinutes,
                    dashboard.TotalEvents);

                return Results.Ok(new ApiResponse<DashboardOverview>("Success", dashboard));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to generate dashboard for window {Window} minutes", query.WindowMinutes);
                return Results.Json(
                    new ApiResponse<object>("Error", null!, "An unexpected error occurred while generating the dashboard."),
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("FetchDashboard")
        .RequireAuthorization(AuthorizationPolicies.DashboardClient)
        .RequireRateLimiting(RateLimitingPolicies.DashboardRead)
        .Produces<ApiResponse<DashboardOverview>>(StatusCodes.Status200OK)
        .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
        .Produces<ApiResponse<object>>(StatusCodes.Status401Unauthorized)
        .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
        .Produces<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)
        .Produces(StatusCodes.Status500InternalServerError);
    }
}

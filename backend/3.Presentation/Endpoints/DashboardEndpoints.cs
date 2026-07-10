using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RealTimeEventAnalyticsEngine.Core.Queries;
using RealTimeEventAnalyticsEngine.Presentation.Responses;

namespace RealTimeEventAnalyticsEngine.Presentation.Endpoints;

/// <summary>
/// Exposed read-only minimal routing interfaces delivering optimized layout aggregates.
/// </summary>
public static class DashboardEndpoints
{
    public static void MapDashboard(this WebApplication app)
    {
        app.MapGet("/api/dashboard", async (
            int windowMinutes,
            FetchDashboardDataQueryHandler handler,
            IValidator<FetchDashboardDataQuery> validator,
            ILogger<FetchDashboardDataQuery> logger) =>
        {
            logger.LogDebug("Dashboard requested with window: {WindowMinutes} minutes", windowMinutes);
            
            var query = new FetchDashboardDataQuery(windowMinutes > 0 ? windowMinutes : 15);
            
            var validation = await validator.ValidateAsync(query);
            if (!validation.IsValid)
            {
                logger.LogWarning("Dashboard query validation failed: {Errors}", 
                    string.Join(", ", validation.Errors.Select(e => e.ErrorMessage)));
                    
                return Results.BadRequest(new ApiResponse<object>("ValidationFailed", null!, 
                    string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));
            }

            try
            {
                var dashboard = await handler.HandleAsync(query);
                logger.LogInformation("Dashboard generated successfully: TotalEvents={TotalEvents}", dashboard.TotalEvents);
                
                return Results.Ok(new ApiResponse<DashboardOverview>("Success", dashboard));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fatal anomaly captured while generating dashboard viewport.");
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("FetchDashboard")
        .Produces<ApiResponse<DashboardOverview>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status500InternalServerError);
    }
}
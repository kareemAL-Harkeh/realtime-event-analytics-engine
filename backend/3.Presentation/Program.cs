using System.Text.Json;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Http.Json;
using Serilog;
using RealTimeEventAnalyticsEngine.Infrastructure.Extensions;
using RealTimeEventAnalyticsEngine.Infrastructure.Logging;
using RealTimeEventAnalyticsEngine.Presentation.Endpoints;
using RealTimeEventAnalyticsEngine.Presentation.Hubs;
using RealTimeEventAnalyticsEngine.Presentation.Middleware;
using RealTimeEventAnalyticsEngine.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog first so startup logs are captured
Log.Logger = SerilogSetup.CreateLoggerConfiguration(builder.Configuration).CreateLogger();
builder.Host.UseSerilog();

// Add core API services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// CORS Configuration
builder.Services.AddCors(options =>
    {
        var allowedOrigins = builder.Configuration["CorsSettings:AllowedOrigins"] ?? "http://localhost:3000,http://localhost:5173,http://localhost:5174";
        var origins = allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(o => o.Trim())
            .ToArray();

        options.AddPolicy("AllowFrontend", policy =>
        {
            policy
                .WithOrigins(origins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });




// Register services using extension methods (Single Responsibility Principle)
builder.Services
    .AddValidationServices()
    .AddCacheServices(builder.Configuration)
    .AddDataServices()
    .AddApplicationHandlers();

var app = builder.Build();

try
{
    var databaseInitializer = app.Services.GetRequiredService<DatabaseInitializer>();
    await databaseInitializer.EnsureDatabaseAndSeedAsync();
}
catch (Exception ex)
{
    Log.Warning(ex, "Database initialization could not complete at startup. The application will continue without seeding data.");
}

// Register performance middleware for sub-millisecond monitoring
app.UseMiddleware<PerformanceLoggingMiddleware>();
app.UseCors("AllowFrontend");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHub<EventHub>("/eventHub");

// Map API endpoints
app.MapEvents();
app.MapDashboard();

try
{
    Log.Information("Starting Real-time Event Analytics Engine");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}


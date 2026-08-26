using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using RealTimeEventAnalyticsEngine.Infrastructure.Data;
using RealTimeEventAnalyticsEngine.Infrastructure.Extensions;
using RealTimeEventAnalyticsEngine.Infrastructure.Logging;
using RealTimeEventAnalyticsEngine.Presentation.Authentication;
using RealTimeEventAnalyticsEngine.Presentation.Endpoints;
using RealTimeEventAnalyticsEngine.Presentation.Extensions;
using RealTimeEventAnalyticsEngine.Presentation.Hubs;
using RealTimeEventAnalyticsEngine.Presentation.Middleware;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = SerilogSetup.CreateLoggerConfiguration(builder.Configuration).CreateLogger();
builder.Host.UseSerilog();

// JSON options
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// OpenAPI + SignalR
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

// Global exception handling - registered here, wired into the pipeline below
// as the very first middleware so it can catch exceptions from everything
// downstream, including PerformanceLoggingMiddleware itself.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Authentication/Authorization - lightweight API key scheme, not a full
// identity provider. See ApiKeyRoles in ApiKeyAuthenticationHandler.cs for why.
builder.Services
    .AddAuthentication(ApiKeyAuthenticationOptions.SchemeName)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationOptions.SchemeName, _ => { });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.IngestionClient, policy =>
        policy.RequireRole(ApiKeyRoles.IngestionClient))
    .AddPolicy(AuthorizationPolicies.DashboardClient, policy =>
        policy.RequireRole(ApiKeyRoles.DashboardClient));

// Rate limiting - policy definitions live in Presentation.Extensions
// (see RateLimitingExtensions.cs for why this isn't in Infrastructure).
// Registered after auth: its partition-key selector now prefers the
// authenticated caller's identity, which only exists once auth has run.
builder.Services.AddRateLimitingPolicies(builder.Configuration);

// CORS
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration["CorsSettings:AllowedOrigins"]
        ?? "http://localhost:3000,http://localhost:5173,http://localhost:5174";

    var origins = allowedOrigins
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Application & Infrastructure services
builder.Services
    .AddValidationServices()
    .AddCacheServices(builder.Configuration)
    .AddDataServices()
    .AddApplicationHandlers();

var app = builder.Build();

// Database initialization (non-blocking for the rest of the app)
try
{
    var initializer = app.Services.GetRequiredService<DatabaseInitializer>();
    await initializer.EnsureDatabaseAndSeedAsync();
}
catch (Exception ex)
{
    Log.Warning(ex, "Database initialization failed at startup. Application will continue.");
}

// Middleware pipeline
// UseExceptionHandler MUST be first: it needs to wrap every other middleware
// (including PerformanceLoggingMiddleware) to be able to catch what they throw.
// Anything registered before this line would NOT be protected by it.
app.UseExceptionHandler();

app.UseMiddleware<PerformanceLoggingMiddleware>();
app.UseCors("AllowFrontend");

// Authentication MUST run before UseRateLimiter: the rate limiter's partition
// key selector reads HttpContext.User to key buckets by authenticated client
// id instead of raw IP (see RateLimitingExtensions.GetPartitionKey) - if this
// ran after the rate limiter, User would still be empty when the bucket is
// chosen and every caller would silently fall back to IP-based partitioning.
app.UseAuthentication();
app.UseAuthorization();

// Placed after CORS so a preflight OPTIONS request never counts against a
// caller's bucket, and after auth for the reason above.
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// SignalR + API endpoints
//
// NOTE: EventHub is intentionally left WITHOUT RequireAuthorization for now.
// SignalR's negotiation handshake supports bearer tokens out of the box via
// accessTokenFactory (sent as ?access_token=... for the transports, like
// WebSockets, that can't set custom headers from a browser) - but that
// mechanism is built around a Bearer scheme, not an arbitrary X-Api-Key
// header. Wiring the two together correctly means either teaching the API key
// scheme to also accept a bearer-style token on the query string, or standing
// up a second scheme just for the hub. That's real work with its own
// failure modes and deserves its own pass, not a rushed addition here that
// would likely just be broken in practice.
app.MapHub<EventHub>("/eventHub");
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
    await Log.CloseAndFlushAsync();
}

public partial class Program { }
namespace RealTimeEventAnalyticsEngine.Presentation.Responses;

public sealed record ApiResponse<T>(string Status, T Data, string? Message = null);

public sealed record ErrorResponse(string Status, string Message, string? TraceId = null);

public sealed record EventAcceptedResponse(string Message = "Event queued for async persistence.");

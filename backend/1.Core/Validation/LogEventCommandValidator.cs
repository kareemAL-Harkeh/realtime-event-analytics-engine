using System.Text.Json;
using FluentValidation;
using RealTimeEventAnalyticsEngine.Core.Commands;

namespace RealTimeEventAnalyticsEngine.Core.Validation;

/// <summary>
/// Enterprise-grade validator for LogEventCommand protecting the engine from malformed telemetry data.
/// </summary>
public sealed class LogEventCommandValidator : AbstractValidator<LogEventCommand>
{
    public LogEventCommandValidator()
    {
        // 1. EventType Validation
        RuleFor(cmd => cmd.EventType)
            .Cascade(CascadeMode.Stop) // Stop validating if the rule fails to save CPU cycles
            .NotEmpty().WithMessage("EventType cannot be null or empty.")
            .Must(type => !string.IsNullOrWhiteSpace(type)).WithMessage("EventType cannot consist of white spaces only.")
            .MaximumLength(100).WithMessage("EventType must not exceed 100 characters.");

        // 2. Payload Validation (Must be structured, valid JSON)
        RuleFor(cmd => cmd.Payload)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Payload payload is required.")
            .MaximumLength(10000).WithMessage("Payload size must not exceed 10,000 characters.")
            .Must(BeAValidJson).WithMessage("Payload must be a valid structured JSON string.");

        // 3. Source Validation
        RuleFor(cmd => cmd.Source)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Source application/service identity is required.")
            .Must(src => !string.IsNullOrWhiteSpace(src)).WithMessage("Source cannot consist of white spaces only.")
            .MaximumLength(200).WithMessage("Source identification must not exceed 200 characters.");

        // 4. High-Precision Timestamp Validation (Time-Window Guarding)
        RuleFor(cmd => cmd.Timestamp)
            .Cascade(CascadeMode.Stop)
            .Must(ts => ts <= DateTimeOffset.UtcNow.AddMinutes(1))
            .WithMessage("Timestamp cannot be more than 1 minute in the future (Clock Skew Guard).")
            .Must(ts => ts >= DateTimeOffset.UtcNow.AddDays(-1))
            .WithMessage("Timestamp cannot be older than 24 hours (Stale data rejection).");
    }

    /// <summary>
    /// Custom rule to efficiently check if a string is parsed into a valid JSON document.
    /// </summary>
    private static bool BeAValidJson(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return false;

        try
        {
            using var jsonDoc = JsonDocument.Parse(payload);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
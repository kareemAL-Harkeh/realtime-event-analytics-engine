using System.Text.Json;
using FluentValidation;
using RealTimeEventAnalyticsEngine.Core.Commands;

namespace RealTimeEventAnalyticsEngine.Core.Validation;

/// <summary>
/// Validates incoming telemetry events before they enter the write queue.
/// Focuses on structural correctness and basic sanity checks.
/// </summary>
public sealed class LogEventCommandValidator : AbstractValidator<LogEventCommand>
{
    public LogEventCommandValidator()
    {
        // EventId is optional, but if provided it should be reasonable
        RuleFor(x => x.EventId)
            .MaximumLength(100)
            .When(x => x.EventId is not null)
            .WithMessage("EventId must not exceed 100 characters.");

        RuleFor(x => x.EventType)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("EventType is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("EventType cannot be whitespace.")
            .MaximumLength(100).WithMessage("EventType must not exceed 100 characters.");

        RuleFor(x => x.Payload)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Payload is required.")
            .MaximumLength(10_000).WithMessage("Payload must not exceed 10,000 characters.")
            .Must(BeValidJson).WithMessage("Payload must be valid JSON.");

        RuleFor(x => x.Source)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Source is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Source cannot be whitespace.")
            .MaximumLength(200).WithMessage("Source must not exceed 200 characters.");

        // NOTE: `default(DateTimeOffset)` (0001-01-01) is our internal signal for
        // "the caller didn't provide a timestamp - let the handler stamp UtcNow later".
        // The handler only runs *after* validation, so if we validated the raw range
        // here unconditionally, every request that omits Timestamp would get rejected
        // as "older than 7 days" before it ever got the chance to be enriched.
        // We explicitly let `default` pass through and only range-check real values.
        RuleFor(x => x.Timestamp)
            .Cascade(CascadeMode.Stop)
            .Must(ts => ts == default || ts <= DateTimeOffset.UtcNow.AddMinutes(5))
            .WithMessage("Timestamp cannot be more than 5 minutes in the future.")
            .Must(ts => ts == default || ts >= DateTimeOffset.UtcNow.AddDays(-7))
            .WithMessage("Timestamp cannot be older than 7 days.");
    }

    private static bool BeValidJson(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        try
        {
            using var _ = JsonDocument.Parse(payload);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

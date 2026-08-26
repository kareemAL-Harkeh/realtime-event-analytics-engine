using FluentValidation.TestHelper;
using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Core.Validation;
using Xunit;

namespace RealTimeEventAnalyticsEngine.Tests.Core.Validation;

/// <summary>
/// Covers LogEventCommandValidator - most importantly the Timestamp rule.
///
/// The very first bug found in this project was here: validation used to run
/// BEFORE LogEventCommandHandler enriched a missing Timestamp to UtcNow, so any
/// event that omitted a Timestamp (leaving it at its default(DateTimeOffset)
/// value) was rejected as "older than 7 days" before the handler ever got a
/// chance to fix it up. The fix let `default` pass through untouched. These
/// tests exist specifically so nobody accidentally "cleans up" that Must(...)
/// clause later and reintroduces the bug without realizing what it breaks.
/// </summary>
public sealed class LogEventCommandValidatorTests
{
    private readonly LogEventCommandValidator _validator = new();

    private static LogEventCommand ValidCommand(DateTimeOffset timestamp = default) => new()
    {
        EventType = "info",
        Payload = "{\"message\":\"ok\"}",
        Source = "order-service",
        Timestamp = timestamp
    };

    [Fact]
    public void Timestamp_Default_DoesNotFailValidation()
    {
        // Regression test for the original bug: an omitted Timestamp (bound
        // from JSON as default(DateTimeOffset)) must pass through untouched
        // so LogEventCommandHandler can enrich it afterward.
        var result = _validator.TestValidate(ValidCommand(default));

        result.ShouldNotHaveValidationErrorFor(x => x.Timestamp);
    }

    [Fact]
    public void Timestamp_WithinAllowedRange_Passes()
    {
        var result = _validator.TestValidate(ValidCommand(DateTimeOffset.UtcNow.AddHours(-1)));

        result.ShouldNotHaveValidationErrorFor(x => x.Timestamp);
    }

    [Fact]
    public void Timestamp_TooFarInTheFuture_Fails()
    {
        var result = _validator.TestValidate(ValidCommand(DateTimeOffset.UtcNow.AddMinutes(10)));

        result.ShouldHaveValidationErrorFor(x => x.Timestamp)
            .WithErrorMessage("Timestamp cannot be more than 5 minutes in the future.");
    }

    [Fact]
    public void Timestamp_OlderThanSevenDays_Fails()
    {
        var result = _validator.TestValidate(ValidCommand(DateTimeOffset.UtcNow.AddDays(-8)));

        result.ShouldHaveValidationErrorFor(x => x.Timestamp)
            .WithErrorMessage("Timestamp cannot be older than 7 days.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EventType_Empty_Fails(string eventType)
    {
        var command = ValidCommand() with { EventType = eventType };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.EventType);
    }

    [Fact]
    public void Payload_InvalidJson_Fails()
    {
        var command = ValidCommand() with { Payload = "{not valid json" };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Payload)
            .WithErrorMessage("Payload must be valid JSON.");
    }

    [Fact]
    public void Payload_ValidJson_Passes()
    {
        var command = ValidCommand() with { Payload = "{\"code\":500}" };

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Payload);
    }

    [Fact]
    public void Source_Whitespace_Fails()
    {
        var command = ValidCommand() with { Source = "   " };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Source);
    }

    [Fact]
    public void EventId_TooLong_Fails()
    {
        var command = ValidCommand() with { EventId = new string('a', 101) };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.EventId);
    }

    [Fact]
    public void FullyValidCommand_HasNoErrors()
    {
        var result = _validator.TestValidate(ValidCommand());

        result.ShouldNotHaveAnyValidationErrors();
    }
}

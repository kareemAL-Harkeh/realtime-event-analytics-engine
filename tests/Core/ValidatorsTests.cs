using FluentAssertions;
using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Core.Queries;
using RealTimeEventAnalyticsEngine.Core.Validation;
using Xunit;

namespace RealTimeEventAnalyticsEngine.Tests.Core;

/// <summary>
/// Core-level unit tests verifying validation invariant rules for both Commands and Queries.
/// </summary>
public class ValidatorsTests
{
    #region LogEventCommand Validation Tests

    [Fact]
    public void LogEventCommandValidator_ShouldAcceptValidCommand_Successfully()
    {
        // Arrange
        var validator = new LogEventCommandValidator();
        var command = new LogEventCommand(
            "info",
            "{\"message\":\"ok\"}",
            "payment-service",
            DateTimeOffset.UtcNow);

        // Act
        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue("because a complete well-formed event payload was provided");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void LogEventCommandValidator_ShouldRejectInvalidJsonPayload()
    {
        // Arrange
        var validator = new LogEventCommandValidator();
        var command = new LogEventCommand(
            "error",
            "{invalid json", // Malformed JSON string
            "payment-service",
            DateTimeOffset.UtcNow);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse("because the raw payload contains syntactically broken JSON");
        result.Errors.Should().ContainSingle(error => error.PropertyName == "Payload")
            .Which.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region FetchDashboardDataQuery Validation Tests

    [Fact]
    public void FetchDashboardDataQueryValidator_ShouldAcceptValidWindow()
    {
        // Arrange
        var validator = new FetchDashboardDataQueryValidator();
        var query = new FetchDashboardDataQuery(60); // Valid 60 minutes window

        // Act
        var result = validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue("because the specified time window falls within realistic analytic bounds");
    }

    [Theory]
    [InlineData(0)]        // Zero limit boundary
    [InlineData(-15)]      // Negative boundary
    [InlineData(500000)]   // Outrageous positive boundary
    public void FetchDashboardDataQueryValidator_ShouldRejectOutOfRangeOrInvalidWindows(int invalidWindowMinutes)
    {
        // Arrange
        var validator = new FetchDashboardDataQueryValidator();
        var query = new FetchDashboardDataQuery(invalidWindowMinutes);

        // Act
        var result = validator.Validate(query);

        result.IsValid.Should().BeFalse($"because {invalidWindowMinutes} minutes is outside allowed limits");
        result.Errors.Should().ContainSingle(error => error.PropertyName == "WindowMinutes");
    }

    #endregion
}
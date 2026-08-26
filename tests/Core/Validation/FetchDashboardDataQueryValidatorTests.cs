using FluentValidation.TestHelper;
using RealTimeEventAnalyticsEngine.Core.Constants;
using RealTimeEventAnalyticsEngine.Core.Queries;
using RealTimeEventAnalyticsEngine.Core.Validation;
using Xunit;

namespace RealTimeEventAnalyticsEngine.Tests.Core.Validation;

/// <summary>
/// Covers the window-bounds check, and specifically that it stays in sync with
/// DashboardWindowDefaults - the constant that replaced three previously
/// duplicated "43200" literals (query default, validator bound, endpoint
/// literal). These tests read the bound FROM the constant rather than
/// hardcoding 43200 again, so if MaxWindowMinutes ever changes, this file
/// doesn't need editing - it keeps testing "the validator matches the shared
/// constant", not "the validator matches today's specific number".
/// </summary>
public sealed class FetchDashboardDataQueryValidatorTests
{
    private readonly FetchDashboardDataQueryValidator _validator = new();

    [Fact]
    public void WindowMinutes_BelowMinimum_Fails()
    {
        var query = new FetchDashboardDataQuery(DashboardWindowDefaults.MinWindowMinutes - 1);

        var result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.WindowMinutes);
    }

    [Fact]
    public void WindowMinutes_AboveMaximum_Fails()
    {
        var query = new FetchDashboardDataQuery(DashboardWindowDefaults.MaxWindowMinutes + 1);

        var result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.WindowMinutes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(43_200)]
    public void WindowMinutes_WithinBounds_Passes(int windowMinutes)
    {
        var query = new FetchDashboardDataQuery(windowMinutes);

        var result = _validator.TestValidate(query);

        result.ShouldNotHaveValidationErrorFor(x => x.WindowMinutes);
    }

    [Fact]
    public void Default_UsesSharedMaxWindow()
    {
        var query = new FetchDashboardDataQuery();

        Assert.Equal(DashboardWindowDefaults.MaxWindowMinutes, query.WindowMinutes);
    }
}

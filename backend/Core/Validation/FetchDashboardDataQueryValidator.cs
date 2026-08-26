using FluentValidation;
using RealTimeEventAnalyticsEngine.Core.Constants;
using RealTimeEventAnalyticsEngine.Core.Queries;

namespace RealTimeEventAnalyticsEngine.Core.Validation;

/// <summary>
/// Ensures that the requested dashboard time window stays within safe bounds.
/// </summary>
public sealed class FetchDashboardDataQueryValidator : AbstractValidator<FetchDashboardDataQuery>
{
    public FetchDashboardDataQueryValidator()
    {
        RuleFor(x => x.WindowMinutes)
            .InclusiveBetween(DashboardWindowDefaults.MinWindowMinutes, DashboardWindowDefaults.MaxWindowMinutes)
            .WithMessage(
                $"WindowMinutes must be between {DashboardWindowDefaults.MinWindowMinutes} " +
                $"and {DashboardWindowDefaults.MaxWindowMinutes} (30 days).");
    }
}

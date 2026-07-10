using FluentValidation;
using RealTimeEventAnalyticsEngine.Core.Queries;

namespace RealTimeEventAnalyticsEngine.Core.Validation;

/// <summary>
/// Validator for the FetchDashboardDataQuery to ensure safe timespan windows are requested.
/// </summary>
public sealed class FetchDashboardDataQueryValidator : AbstractValidator<FetchDashboardDataQuery>
{
    private const int MinWindowMinutes = 1;
    private const int MaxWindowMinutes = 43200;

    public FetchDashboardDataQueryValidator()
    {
        RuleFor(query => query.WindowMinutes)
            .InclusiveBetween(MinWindowMinutes, MaxWindowMinutes)
            .WithMessage($"The requested time window must be between {MinWindowMinutes} and {MaxWindowMinutes} minutes.");
    }
}


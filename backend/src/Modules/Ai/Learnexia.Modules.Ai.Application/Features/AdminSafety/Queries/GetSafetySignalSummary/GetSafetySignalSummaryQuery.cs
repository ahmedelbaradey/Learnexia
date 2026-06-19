using Learnexia.Modules.Ai.Application.Features.AdminSafety.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Ai.Application.Features.AdminSafety.Queries.GetSafetySignalSummary;

/// <summary>
/// Aggregates AI safety-event signals over a date range (P7-11 AC1).
///
/// Queries are NOT auto-validated (ValidationBehavior runs for ICommand only).
/// Range validation is performed in the handler.
/// </summary>
public record GetSafetySignalSummaryQuery : IQuery<BaseResponse<SafetySignalSummaryDto>>
{
    /// <summary>Inclusive lower bound (UTC). Defaults to 30 days ago.</summary>
    public DateTime? From { get; init; }

    /// <summary>Inclusive upper bound (UTC). Defaults to now.</summary>
    public DateTime? To { get; init; }
}

using Learnexia.Modules.Ai.Application.Features.AdminTutorUsage.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Ai.Application.Features.AdminTutorUsage.Queries.GetTutorUsage;

/// <summary>
/// Aggregates AI tutor usage/cost metrics over a date range (P7-11 usage dashboard).
///
/// Queries are NOT auto-validated (ValidationBehavior runs for ICommand only).
/// Range validation is performed in the handler.
/// </summary>
public record GetTutorUsageQuery : IQuery<BaseResponse<TutorUsageDto>>
{
    /// <summary>Inclusive lower bound (UTC). Defaults to 30 days ago.</summary>
    public DateTime? From { get; init; }

    /// <summary>Inclusive upper bound (UTC). Defaults to now.</summary>
    public DateTime? To { get; init; }
}

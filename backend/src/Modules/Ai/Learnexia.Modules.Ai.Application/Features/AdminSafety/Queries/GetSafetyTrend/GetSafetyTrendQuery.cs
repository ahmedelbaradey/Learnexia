using Learnexia.Modules.Ai.Application.Features.AdminSafety.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Ai.Application.Features.AdminSafety.Queries.GetSafetyTrend;

/// <summary>
/// Returns per-day safety event counts over a date range (P7-11 AC3).
///
/// Queries are NOT auto-validated; range validation is performed in the handler.
/// </summary>
public record GetSafetyTrendQuery : IQuery<BaseResponse<IReadOnlyList<SafetyTrendBucketDto>>>
{
    /// <summary>Inclusive lower bound (UTC). Defaults to 30 days ago.</summary>
    public DateTime? From { get; init; }

    /// <summary>Inclusive upper bound (UTC). Defaults to now.</summary>
    public DateTime? To { get; init; }
}

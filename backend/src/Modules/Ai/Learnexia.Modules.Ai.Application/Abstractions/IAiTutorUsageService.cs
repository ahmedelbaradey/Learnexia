using Learnexia.Modules.Ai.Application.Features.AdminTutorUsage.Dtos;

namespace Learnexia.Modules.Ai.Application.Abstractions;

/// <summary>
/// Application-layer seam for the AI tutor usage/cost admin read model (P7-11).
///
/// The implementation (in Infrastructure) owns the <c>AiDbContext</c>, EF query composition,
/// and all in-memory aggregation. Callers receive fully-materialised results —
/// no <c>IQueryable</c> or EF types ever cross this boundary (Option-C standard).
///
/// <para>From/To are always resolved and validated (from &lt; to) before calling these methods.</para>
/// </summary>
public interface IAiTutorUsageService
{
    /// <summary>
    /// Aggregates AI usage/cost metrics over the supplied date range.
    /// From/To are already resolved (non-null, from &lt; to) before calling this method.
    /// Returns a zeroed <see cref="TutorUsageDto"/> when the window contains no rows.
    /// </summary>
    Task<TutorUsageDto> GetUsageSummaryAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);
}

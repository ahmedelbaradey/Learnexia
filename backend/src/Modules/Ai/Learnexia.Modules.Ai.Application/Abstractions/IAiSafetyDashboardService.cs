using Learnexia.Modules.Ai.Application.Features.AdminSafety.Dtos;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Ai.Application.Abstractions;

/// <summary>
/// Application-layer seam for the AI-safety admin read model (P7-11).
///
/// The implementation (in Infrastructure) owns the <c>AiDbContext</c>, EF query composition,
/// and all in-memory aggregation of jsonb array fields. Callers receive fully-materialised
/// results — no <c>IQueryable</c> or EF types ever cross this boundary (Option C standard).
///
/// <para>jsonb aggregation strategy (OQ-jsonb, brief §backend-feature handoff):
/// <c>ReasonCodes</c> and <c>FailedChecks</c> are stored as jsonb string arrays. Because
/// PostgreSQL jsonb unnesting is not available through EF Core LINQ translation for this
/// Npgsql version without raw SQL, the implementation fetches the date-windowed result set
/// (which is bounded and small for a monitoring dashboard) and aggregates in-memory.
/// This is safe and correct for v1 given the expected volume; document the choice here so a
/// future owner can replace with a raw-SQL <c>jsonb_array_elements_text</c> unnest if the
/// table grows large.</para>
/// </summary>
public interface IAiSafetyDashboardService
{
    /// <summary>
    /// Aggregates safety signals over the supplied date range.
    /// From/To are already resolved (non-null, from &lt; to) before calling this method.
    /// </summary>
    Task<SafetySignalSummaryDto> GetSummaryAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a paged, newest-first list of flagged AI outputs.
    /// pageNumber ≥ 1, pageSize 1–100 are clamped by the caller before this method is invoked.
    /// </summary>
    Task<PaginatedResult<FlaggedOutputDto>> GetFlaggedOutputsPagedAsync(
        string? action,
        string? reasonCode,
        string? taskKind,
        DateTime? from,
        DateTime? to,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns per-day safety event counts over the date range.
    /// From/To are already resolved (non-null, from &lt; to) before calling this method.
    /// </summary>
    Task<IReadOnlyList<SafetyTrendBucketDto>> GetTrendAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);
}

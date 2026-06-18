namespace Learnexia.Modules.Learning.Application.Services;

/// <summary>
/// In-module service seam for the P5-09 per-student recommendation computation + persistence.
/// Handlers and the <see cref="RecommendationRecomputeJob"/> inject this interface —
/// never EF directly (Option C).
///
/// <para>Implements the "upsert once per day" contract: calling <c>ComputeAndUpsertAsync</c>
/// for the same <c>(studentId, date)</c> pair is idempotent — the second call overwrites
/// the first with the same (deterministic) output.</para>
///
/// <para>Cold-start / empty safety: a student with no weak areas (first-week / no-attempts)
/// returns a well-formed encouraging set, never an error (mirrors P5-02 AC3).</para>
///
/// <para>P5-09-BE-3.</para>
/// </summary>
public interface IRecommendationService
{
    /// <summary>
    /// Computes the daily recommendation set for the given student and upserts it
    /// into the <c>StudentRecommendations</c> table (idempotent per <c>(studentId, date)</c>).
    /// </summary>
    /// <param name="studentId">Loose int reference to the Identity student user.</param>
    /// <param name="date">
    /// The calendar day to compute for. Normally <c>DateOnly.FromDateTime(DateTime.UtcNow)</c>;
    /// passed as a parameter so callers (tests) can control the date.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task ComputeAndUpsertAsync(int studentId, DateOnly date, CancellationToken ct = default);
}

using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Pure static domain service for computing per-skill mastery.
///
/// CONTRACT (Q1 + Q2 + Q7 from P3-09 brief):
/// - Formula: cumulative accuracy = round(correctAnswers / totalAnswers * 100).
///   Identical to <c>GetSkillStatsQueryHandler</c> / P2-04 transient aggregate — no silent divergence (Q5).
/// - <b>No Strategy/State pattern.</b> A plain static method + enum is the design (CLAUDE rule 8).
/// - No DB, no DI. Caller pre-fetches totalAnswers, correctAnswers, and the per-skill MasteryThreshold.
/// - MasteryThreshold is an int 0..100 representing a percentage (same contract P2-04 honours).
/// - Thread-safe: no shared mutable state.
///
/// State machine (Q2 — per-skill threshold + 50% floor):
///   <c>NotStarted</c>   — totalAnswers == 0 (no answers for the skill).
///   <c>NeedsReview</c>  — answered, masteryPercentage &lt; 50 (FR-AD-3 remediation bar; fixed floor).
///   <c>InProgress</c>   — 50 ≤ masteryPercentage &lt; masteryThreshold.
///   <c>Mastered</c>     — masteryPercentage ≥ masteryThreshold (per-skill seeded value 70–85 %;
///                         FR-AD-3 "80%" is the default; per-skill values are deliberate overrides).
/// </summary>
public static class MasteryEngine
{
    /// <summary>
    /// Computes mastery percentage and status from cumulative answer aggregates.
    /// </summary>
    /// <param name="totalAnswers">Total answers submitted by the student for this skill (≥ 0).</param>
    /// <param name="correctAnswers">Correct answers out of <paramref name="totalAnswers"/> (0 ≤ x ≤ total).</param>
    /// <param name="masteryThreshold">
    ///     Per-skill mastery threshold in the range [0, 100] (e.g. 70, 75, 80, 85).
    ///     Retrieved from <c>Skill.MasteryThreshold</c>. When absent, use 80 (FR-AD-3 default).
    /// </param>
    /// <returns>
    ///     A value tuple of:
    ///     <list type="bullet">
    ///         <item><c>masteryPercentage</c> — integer [0, 100], rounded from cumulative accuracy.</item>
    ///         <item><c>status</c> — the resulting <see cref="MasteryStatus"/> per the state machine.</item>
    ///     </list>
    /// </returns>
    public static (int masteryPercentage, MasteryStatus status) Compute(
        int totalAnswers,
        int correctAnswers,
        int masteryThreshold)
    {
        // Guard: no answers → NotStarted (state machine entry point).
        if (totalAnswers == 0)
            return (0, MasteryStatus.NotStarted);

        // Cumulative accuracy formula — same as GetSkillStatsQueryHandler / P2-04 (Q1 / Q5).
        var masteryPercentage = (int)Math.Round((double)correctAnswers / totalAnswers * 100);

        // State machine (Q2 — per-skill threshold + fixed 50% floor):
        var status = masteryPercentage >= masteryThreshold
            ? MasteryStatus.Mastered
            : masteryPercentage >= 50
                ? MasteryStatus.InProgress
                : MasteryStatus.NeedsReview;

        return (masteryPercentage, status);
    }
}

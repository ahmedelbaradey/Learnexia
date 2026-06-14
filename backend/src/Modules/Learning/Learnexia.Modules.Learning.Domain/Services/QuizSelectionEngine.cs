using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Pure static domain service for selecting a difficulty-weighted question set from a candidate
/// pool (P3-11-BE-3, FR-QZ-3).
///
/// CONTRACT:
/// - Pure static — no database, no DI. Caller pre-fetches candidates and resolves options
///   from DI before calling this method. Mirrors <see cref="AdaptivityEngine"/>.
/// - <b>No Strategy/Factory pattern</b> (CLAUDE rule 8). Plain pure function + options + enum.
/// - Thread-safe: no shared mutable state.
/// - Deterministic and reproducible: candidates are sorted by <c>Id</c> before mixing so that
///   the same inputs always produce the same ordered output — enabling resume reproducibility (AC4).
///
/// SELECTION POLICY (Q2, P3-11 brief — approved weighted-mix):
///   Given <c>target</c> difficulty, return approximately:
///     ~targetRatio fraction from the target bucket (Hard→70 %, Medium→60 %, Easy→70 %),
///     ~blendRatio  fraction from adjacent buckets.
///   "Adjacent" means: Hard→Medium; Easy→Medium; Medium→Hard+Easy equally.
///   Counts are computed proportionally and always sum to ≤ pool size (no duplicates).
///
/// GRACEFUL DEGRADATION (Q5):
///   - Target bucket empty → serve the full sorted candidate pool (never throw; caller logs a
///     content-gap warning server-side).
///   - Total pool empty → return empty list (never throw).
///   - Pool smaller than the proportion split → serve whatever exists without error.
///
/// DETERMINISM (Q4):
///   Candidates are sorted ascending by <c>Id</c> before partitioning and slicing. Given identical
///   candidate IDs + target + options, the output list is always the same ordered subset.
/// </summary>
public static class QuizSelectionEngine
{
    /// <summary>
    /// Selects a difficulty-weighted subset of questions from the given candidate pool.
    /// </summary>
    /// <param name="candidates">
    ///     The full Published+Active question pool for the lesson (caller-filtered).
    ///     Must not be null; may be empty.
    /// </param>
    /// <param name="target">
    ///     The recommended <see cref="DifficultyLevel"/> from <c>IAdaptivityService.GetTargetDifficulty</c>.
    /// </param>
    /// <param name="options">
    ///     Tunable mix ratios (resolved from <c>IOptions&lt;QuizSelectionOptions&gt;</c> by the caller).
    /// </param>
    /// <returns>
    ///     An ordered, deterministic subset of <paramref name="candidates"/> weighted toward
    ///     <paramref name="target"/> difficulty. Never null. May be empty only if
    ///     <paramref name="candidates"/> is empty.
    /// </returns>
    public static IReadOnlyList<QuizQuestion> Select(
        IReadOnlyList<QuizQuestion> candidates,
        DifficultyLevel target,
        QuizSelectionOptions options)
    {
        // ── Empty pool → return empty (never throw) ───────────────────────────────────────────────
        if (candidates.Count == 0)
            return Array.Empty<QuizQuestion>();

        // ── Deterministic stable sort by Id (AC4 resume reproducibility) ─────────────────────────
        var sorted = candidates.OrderBy(q => q.Id).ToList();

        // ── Partition into difficulty buckets ─────────────────────────────────────────────────────
        var easyBucket   = sorted.Where(q => q.Difficulty == DifficultyLevel.Easy).ToList();
        var mediumBucket = sorted.Where(q => q.Difficulty == DifficultyLevel.Medium).ToList();
        var hardBucket   = sorted.Where(q => q.Difficulty == DifficultyLevel.Hard).ToList();

        // ── Identify target + adjacent buckets ────────────────────────────────────────────────────
        List<QuizQuestion> targetBucket;
        List<List<QuizQuestion>> adjacentBuckets;
        double targetRatio;

        switch (target)
        {
            case DifficultyLevel.Hard:
                targetBucket    = hardBucket;
                adjacentBuckets = new List<List<QuizQuestion>> { mediumBucket };
                targetRatio     = Math.Clamp(options.HardRatio, 0.0, 1.0);
                break;

            case DifficultyLevel.Easy:
                targetBucket    = easyBucket;
                adjacentBuckets = new List<List<QuizQuestion>> { mediumBucket };
                targetRatio     = Math.Clamp(options.EasyRatio, 0.0, 1.0);
                break;

            default: // Medium — adjacent = both Easy and Hard
                targetBucket    = mediumBucket;
                adjacentBuckets = new List<List<QuizQuestion>> { easyBucket, hardBucket };
                targetRatio     = Math.Clamp(options.MediumRatio, 0.0, 1.0);
                break;
        }

        // ── Graceful degradation: target bucket empty → serve full sorted pool ────────────────────
        // The caller (StartAttemptCommandHandler) logs a content-gap warning when this occurs.
        if (targetBucket.Count == 0)
            return sorted;

        // ── Compute how many to take from target vs. adjacent ─────────────────────────────────────
        // We return all available questions (no artificial cap on the count) — the mix is a ratio
        // of what the pool contains, not a fixed-N selection.
        int total = sorted.Count;

        // How many from the target bucket (at least 1, at most all of target bucket).
        int targetCount = (int)Math.Round(total * targetRatio);
        targetCount = Math.Clamp(targetCount, 1, targetBucket.Count);

        // Remaining slots go to adjacent buckets proportionally.
        int remainingSlots = total - targetCount;

        // Collect adjacent questions (already in sorted order since we partitioned sorted).
        var allAdjacent = adjacentBuckets.SelectMany(b => b).ToList();
        int adjacentCount = Math.Min(remainingSlots, allAdjacent.Count);

        // ── Build the result list (deterministic: target first, adjacent after) ──────────────────
        var result = new List<QuizQuestion>(targetCount + adjacentCount);
        result.AddRange(targetBucket.Take(targetCount));
        result.AddRange(allAdjacent.Take(adjacentCount));

        // Final sort by Id to preserve deterministic ordering of the combined set.
        result.Sort((a, b) => a.Id.CompareTo(b.Id));

        return result;
    }
}

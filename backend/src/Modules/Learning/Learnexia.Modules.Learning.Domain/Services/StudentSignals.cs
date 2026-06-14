using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Pre-aggregated input signals for <see cref="StudentProfileEngine.Derive"/>.
///
/// Assembled by <c>StudentProfileService.RecomputeProfile</c> from raw repository queries
/// so the engine itself stays pure (no DB, no DI). Mirrors the pattern established by
/// <see cref="AdaptivitySignals"/> for the adaptivity engine.
///
/// All lists are immutable (IReadOnlyList / IReadOnlyList) so the engine cannot accidentally
/// mutate the input.
/// </summary>
/// <param name="AnswersByType">
///     Per-QuestionType aggregate counts. Each element carries the type enum value, the number
///     of correct answers, and the total answers for that type for this student.
///     Types with no answers must be absent from this list (not zero-row entries).
/// </param>
/// <param name="SkillErrorCounts">
///     Per-skill wrong-answer counts. Each element carries the SkillId and the total number
///     of incorrect answers recorded for that skill for this student. Only skills with at least
///     one wrong answer are included.
/// </param>
/// <param name="SessionAccuracyBuckets">
///     Ordered sequence of (minuteBucket, accuracy) pairs derived from Attempt/StudentAnswer
///     timestamps. Each minuteBucket is the elapsed-minute window (0, 20, 40, …) and accuracy
///     is the fraction of correct answers in that window [0.0 .. 1.0].
///     May be empty when insufficient data is available.
/// </param>
/// <param name="OverallAccuracy">
///     Student's overall answer accuracy across ALL question types, in [0.0 .. 1.0].
///     Used as the baseline when computing per-type affinity.
/// </param>
/// <param name="TotalAnswers">
///     Total number of answers submitted by this student. Used for cold-start detection
///     (compared against <see cref="StudentProfileOptions.ColdStartDataPointThreshold"/>).
/// </param>
/// <param name="HintAnswerCountByType">
///     Per-QuestionType hint-usage counts. Each element carries the type enum value and the
///     number of answers in that type where a hint was used. Used by
///     <c>DeriveExplanationStyle</c> to detect hint-heavy patterns.
///     Types with no hint usage are absent.
/// </param>
public record StudentSignals(
    IReadOnlyList<(QuestionType Type, int Correct, int Total)> AnswersByType,
    IReadOnlyList<(int SkillId, int WrongCount)> SkillErrorCounts,
    IReadOnlyList<(int MinuteBucket, double Accuracy)> SessionAccuracyBuckets,
    double OverallAccuracy,
    int TotalAnswers,
    IReadOnlyList<(QuestionType Type, int HintCount, int Total)> HintAnswerCountByType);

using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Output of <see cref="StudentProfileEngine.Derive"/> — the four AC1 behavioral attributes
/// computed from <see cref="StudentSignals"/> plus a cold-start confidence indicator.
///
/// All properties use immutable collection types so callers cannot accidentally mutate the result.
///
/// On the cold-start path (<see cref="StudentSignals.TotalAnswers"/> &lt; threshold), the engine
/// returns this record with empty/null/Standard defaults and <see cref="DataPointCount"/> == 0.
/// Consumers (P3-03 prompt builder, P3-08 adaptivity engine) must treat DataPointCount below the
/// threshold as "insufficient data — do not adapt yet."
/// </summary>
/// <param name="QuestionTypeAffinity">
///     Per-QuestionType success-rate map. Keys are <see cref="QuestionType"/> enum names;
///     values are the student's success rate [0.0 .. 1.0] for that type.
///     Only types that exceeded the min-sample guard appear in this map.
///     Empty dictionary on cold-start.
/// </param>
/// <param name="RecurringErrorSkillIds">
///     SkillIds where the student has repeated wrong-answer patterns (≥ threshold wrong answers).
///     Empty list when no recurring errors are detected or on cold-start.
/// </param>
/// <param name="AttentionSpanMinutes">
///     Estimated number of minutes into a session before accuracy noticeably drops.
///     Null when insufficient data exists (cold-start or not enough session data).
/// </param>
/// <param name="PreferredExplanationStyle">
///     Derived explanation-style preference. <see cref="ExplanationStyle.Standard"/> on cold-start
///     or when the heuristic cannot distinguish a pattern.
/// </param>
/// <param name="DataPointCount">
///     Number of answer data points that fed this derivation. Zero on cold-start. Used by
///     consumers as a confidence indicator.
/// </param>
/// <param name="GritScore">
///     P3-13a — Behavioral persistence / grit proxy in [0.0 .. 1.0].
///     Blends two signals: the fraction of skills where the student retried after a wrong
///     answer (<c>RetryAfterWrongRate</c>) and an inverted overall hint rate (i.e. lower
///     hint reliance = higher self-sufficiency = higher grit contribution). A score near 1.0
///     means the student pushes through hard problems with low hint reliance; near 0.0 means
///     high hint-dependence and low retry behavior.
///     Null on cold-start or when <c>TotalAnswers &lt; MinSampleForGrit</c>.
/// </param>
/// <param name="MasteryVelocity">
///     P3-13a — Rate-of-improvement across attempts (mastery trajectory slope), in [-1.0 .. 1.0].
///     Computed as the difference between the average accuracy of the most-recent fraction of
///     attempts and the oldest fraction (controlled by
///     <c>StudentProfileOptions.TrajectoryRecentWindowFraction</c>). Positive = improving,
///     negative = regressing, near zero = stable. Null on cold-start or when fewer than
///     <c>MinAttemptsForTrajectory</c> attempts exist.
/// </param>
public record DerivedProfile(
    IReadOnlyDictionary<string, double> QuestionTypeAffinity,
    IReadOnlyList<int> RecurringErrorSkillIds,
    int? AttentionSpanMinutes,
    ExplanationStyle PreferredExplanationStyle,
    int DataPointCount,
    double? GritScore,
    double? MasteryVelocity);

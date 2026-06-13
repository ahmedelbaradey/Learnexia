namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Configuration for the Student Profile Engine (P3-13). Bound from
/// <c>appsettings.json: StudentProfile:Engine</c>.
///
/// Placed in the Domain layer (alongside the pure engine it configures) so that
/// <see cref="StudentProfileEngine"/> can reference it without violating the
/// Application → Domain dependency direction. Mirrors the pattern established by
/// <see cref="AdaptivityOptions"/> and <see cref="SpacedRepetitionOptions"/>.
///
/// All thresholds are tunables — product has not specified exact values; these are
/// defensible v1 defaults (flagged in HANDOFF for review).
/// </summary>
public sealed class StudentProfileOptions
{
    /// <summary>Config section key — <c>"StudentProfile:Engine"</c>.</summary>
    public const string SectionName = "StudentProfile:Engine";

    /// <summary>
    /// Minimum number of answers required per QuestionType before that type is considered
    /// for affinity scoring. Types with fewer answers than this are excluded from the
    /// affinity map (insufficient sample — not reliable). Default: 5.
    /// </summary>
    public int MinSamplePerQuestionType { get; set; } = 5;

    /// <summary>
    /// Minimum number of wrong answers on the same skill before it is classified as a
    /// recurring-error cluster. Default: 3 (three incorrect answers on a skill = pattern).
    /// </summary>
    public int RecurringErrorThreshold { get; set; } = 3;

    /// <summary>
    /// Accuracy-drop threshold that triggers the attention-span / fatigue signal.
    /// When accuracy in a later minute-bucket falls below the session-start accuracy by this
    /// fraction, the engine records that minute-bucket as the attention-span limit.
    /// Default: 0.15 (15% drop from session-start accuracy).
    /// </summary>
    public double AccuracyDropThreshold { get; set; } = 0.15;

    /// <summary>
    /// Width of each time bucket used for attention-span derivation, in minutes.
    /// Answers within an attempt are bucketed into windows of this many minutes.
    /// Default: 20 (each bucket covers 20 minutes of elapsed attempt time).
    /// </summary>
    public int AttentionWindowMinutes { get; set; } = 20;

    /// <summary>
    /// Total-answers threshold below which the profile is treated as cold-start.
    /// When <c>StudentSignals.TotalAnswers &lt; ColdStartDataPointThreshold</c>, the engine
    /// returns neutral default values for all four attributes and skips derivation.
    /// Default: 10.
    /// </summary>
    public int ColdStartDataPointThreshold { get; set; } = 10;
}

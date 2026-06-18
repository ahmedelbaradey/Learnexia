namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Configuration for the Recommendation Engine (P5-09a). Bound from
/// <c>appsettings.json: Recommendations:Engine</c>.
///
/// Placed in the Domain layer (alongside the pure engine it configures) so that
/// <see cref="RecommendationEngine"/> can reference it without violating the
/// Application → Domain dependency direction. Mirrors the pattern established by
/// <see cref="AdaptivityOptions"/> and <see cref="StudentProfileOptions"/>.
///
/// All thresholds are product-tunables — defaults are defensible v1 values (see
/// HANDOFF for review notes). Product can tune any of these without a redeploy.
/// </summary>
public sealed class RecommendationOptions
{
    /// <summary>Config section key — <c>"Recommendations:Engine"</c>.</summary>
    public const string SectionName = "Recommendations:Engine";

    /// <summary>
    /// Attention-span (fatigue) threshold in minutes.
    /// When <c>DerivedProfile.AttentionSpanMinutes</c> is non-null and
    /// <c>≤ AttentionSpanFatigueThresholdMinutes</c>, the engine caps the output set
    /// nearer <see cref="FatigueQuantityCap"/> rather than <see cref="MaxItems"/>.
    /// Default: 20 minutes — matches P3-13's <c>AttentionWindowMinutes</c> default.
    /// </summary>
    public int AttentionSpanFatigueThresholdMinutes { get; set; } = 20;

    /// <summary>
    /// Maximum number of recommendation items returned to a fatigue-prone child
    /// (i.e. when <c>AttentionSpanMinutes ≤ AttentionSpanFatigueThresholdMinutes</c>).
    /// Must be ≤ <see cref="MaxItems"/>. Default: 3.
    /// </summary>
    public int FatigueQuantityCap { get; set; } = 3;

    /// <summary>
    /// Minimum number of items always returned (non-cold-start path).
    /// Ensures the child always has at least this many actionable recommendations.
    /// Default: 3.
    /// </summary>
    public int MinItems { get; set; } = 3;

    /// <summary>
    /// Maximum number of items in a normal (non-fatigue) recommendation set.
    /// The engine caps the sorted weak-area list to this value.
    /// Default: 5 — matches the P5-09 spec cap.
    /// </summary>
    public int MaxItems { get; set; } = 5;

    /// <summary>
    /// <c>DataPointCount</c> threshold below which the engine uses only the most
    /// conservative profile rule (RecurringError → Review) and falls back to
    /// grade + mastery for everything else. Zero means cold-start (unchanged behaviour).
    ///
    /// Reuses the same threshold value as <see cref="StudentProfileOptions.ColdStartDataPointThreshold"/>
    /// (default 10) so the two thresholds stay in sync by default, but they can be tuned
    /// independently if product requires different confidence gates.
    /// Default: 10.
    /// </summary>
    public int ConfidenceDataPointThreshold { get; set; } = 10;

    /// <summary>
    /// When <c>true</c>, the engine applies a bounded difficulty nudge toward the low
    /// edge of the adaptivity band on <c>Review</c> items for fatigue-prone children.
    /// The nudge is always bounded: it moves difficulty from <c>Medium</c> → <c>Easy</c>
    /// or <c>Hard</c> → <c>Medium</c> on <c>Review</c> items only — it NEVER crosses
    /// the AdaptivityEngine's assigned band (<c>GetTargetDifficulty</c> stays source of truth).
    /// Default: true.
    /// </summary>
    public bool DifficultyNudgeEnabled { get; set; } = true;
}

namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Configuration for the Calibration Engine (P5-07). Bound from
/// <c>appsettings.json: Calibration:Engine</c>.
///
/// Placed in the Domain layer (alongside the pure engine it configures) so that
/// <see cref="CalibrationEngine"/> can reference it without violating the
/// Application → Domain dependency direction. Mirrors the pattern established by
/// <see cref="RecommendationOptions"/> and <see cref="AdaptivityOptions"/>.
///
/// All thresholds are product-tunables — defaults are the LOCKED v1 values agreed in the
/// P5-07 Execution Plan (2026-06-21). Config section overrides any of these without a redeploy.
/// </summary>
public sealed class CalibrationOptions
{
    /// <summary>Config section key — <c>"Calibration:Engine"</c>.</summary>
    public const string SectionName = "Calibration:Engine";

    /// <summary>
    /// Minimum number of <c>StudentAnswer</c> rows required before a question is considered
    /// statistically meaningful. Gates both proposal generation and AI-flag logic.
    /// Default: 30 (LOCKED v1 — conservatively high to prevent early-cohort noise).
    /// </summary>
    public int MinSamples { get; set; } = 30;

    /// <summary>
    /// P-value (fraction correct, 0.0–1.0) at or above which the question is empirically Easy.
    /// High p-value = many students answered correctly = the question is easy in practice.
    /// Default: 0.85 (LOCKED v1).
    /// </summary>
    public double EasyThreshold { get; set; } = 0.85;

    /// <summary>
    /// P-value (fraction correct, 0.0–1.0) at or below which the question is empirically Hard.
    /// Low p-value = few students answered correctly = the question is hard in practice.
    /// Default: 0.45 (LOCKED v1). Medium band is between HardThreshold and EasyThreshold.
    /// </summary>
    public double HardThreshold { get; set; } = 0.45;

    /// <summary>
    /// P-value at or above which an AI-generated question is flagged for review (extremely easy).
    /// Applied only when <c>GeneratedBy != Curated</c> and <c>SampleSize >= MinSamples</c>.
    /// Default: 0.95 (LOCKED v1) — nearly every student got it right (suspiciously trivial for AI).
    /// </summary>
    public double AiFlagHighPValue { get; set; } = 0.95;

    /// <summary>
    /// P-value at or below which an AI-generated question is flagged for review (extremely hard).
    /// Applied only when <c>GeneratedBy != Curated</c> and <c>SampleSize >= MinSamples</c>.
    /// Default: 0.20 (LOCKED v1) — very few students got it right (suspiciously hard or broken).
    /// </summary>
    public double AiFlagLowPValue { get; set; } = 0.20;

    /// <summary>
    /// Cron expression for the daily calibration job.
    /// Default: "20 0 * * *" = 00:20 UTC daily — runs AFTER Rec-Recompute (00:05 UTC) and
    /// SP-Recompute (00:00 UTC) so the behavioral profile signals are fresh.
    /// </summary>
    public string JobCronExpression { get; set; } = "20 0 * * *";
}

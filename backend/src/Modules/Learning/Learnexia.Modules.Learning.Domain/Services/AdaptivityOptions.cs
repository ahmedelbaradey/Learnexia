namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Configuration for the Adaptivity Engine (P3-08). Bound from <c>appsettings.json:Adaptivity:Engine</c>.
///
/// Placed in the Domain layer (alongside the pure engine it configures) so that
/// <see cref="AdaptivityEngine"/> can reference it without violating the
/// Application → Domain dependency direction.
///
/// Q1 (P3-08 brief) — approved algorithm: deterministic weighted-score band.
///   <c>performanceScore = WeightAccuracy·acc + WeightTime·timeNorm + WeightHint·(1−hintRate) + WeightRetry·(1−retryNorm)</c>
///   Band: score ≥ HighBand → Hard; score ≤ LowBand → Easy; else Medium.
///
/// Defaults are sensible v1 values (accuracy-dominant, low time weight to degrade gracefully
/// when per-skill expected baseline is unavailable — Q3). All values can be tuned via config
/// without a redeploy.
///
/// P3-13 optional fatigue hook: <c>FatigueSignalWeight</c> defaults 0 — no effect until P3-13 wires in a value.
/// </summary>
public sealed class AdaptivityOptions
{
    /// <summary>Config section key — <c>"Adaptivity:Engine"</c>.</summary>
    public const string SectionName = "Adaptivity:Engine";

    /// <summary>Weight for the accuracy signal. Default: 0.5 (accuracy-dominant per Q1).</summary>
    public double WeightAccuracy { get; set; } = 0.5;

    /// <summary>
    /// Weight for the response-time signal. Default: 0.2.
    /// When AvgTimeSeconds ≤ 0 the time component is zeroed (graceful degradation — Q3).
    /// </summary>
    public double WeightTime { get; set; } = 0.2;

    /// <summary>
    /// Weight for the hint-usage signal. Default: 0.2.
    /// Higher hint rate lowers the score: component = WeightHint * (1 − hintRate).
    /// </summary>
    public double WeightHint { get; set; } = 0.2;

    /// <summary>
    /// Weight for the retry/attempt-count signal. Default: 0.1.
    /// Higher retry count lowers the score: component = WeightRetry * (1 − retryNorm).
    /// </summary>
    public double WeightRetry { get; set; } = 0.1;

    /// <summary>Score threshold above which the engine returns Hard. Default: 0.8.</summary>
    public double HighBand { get; set; } = 0.8;

    /// <summary>Score threshold at or below which the engine returns Easy. Default: 0.5.</summary>
    public double LowBand { get; set; } = 0.5;

    /// <summary>
    /// Baseline for retry normalization: ≤1 attempt = full score, degrades with each extra attempt.
    /// Default: 2.
    /// </summary>
    public int ExpectedAttempts { get; set; } = 2;

    /// <summary>
    /// P3-13 optional fatigue/attention signal weight. Default: 0 (no effect until P3-13 is built).
    /// When non-zero, the fatigue signal is subtracted from the performance score before banding.
    /// </summary>
    public double FatigueSignalWeight { get; set; } = 0.0;

    /// <summary>
    /// Reference baseline (seconds) for time normalization (Q3): an average answer time at this
    /// baseline yields a time score of 1.0; slower answers score proportionally lower (capped [0,1]).
    /// Default: 30. Externalized for P5-07 BE-4 / AC3 so the last adaptivity tunable is config-driven
    /// (no hardcoded thresholds remain). A non-positive configured value is ignored — the engine
    /// falls back to 30 — to avoid divide-by-zero / nonsensical normalization.
    /// </summary>
    public double ExpectedTimeSecondsBaseline { get; set; } = 30.0;
}

namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Pre-fetched input vector for the <see cref="AdaptivityEngine"/>.
///
/// All four FR-AD-1 signals are packed here by <c>AdaptivityService</c> before calling the engine.
/// The engine itself has no DB/DI dependencies — it operates on this plain value record only.
///
/// Signal meanings (all normalized to [0,1] by the engine caller or degraded gracefully):
///   <c>AccuracyPct</c>   — cumulative accuracy [0.0, 1.0] (correct/total from StudentAnswer rows).
///   <c>AvgTimeSeconds</c> — mean time per answer in seconds (≤0 means unavailable → time degraded to 0).
///   <c>HintRate</c>       — fraction of answers that used a hint [0.0, 1.0].
///   <c>RetryCount</c>     — total retry/attempt count beyond baseline.
///   <c>MasteryPct</c>     — latest persisted <c>StudentSkillMastery.MasteryPercentage</c> [0, 100].
///   <c>IsDefault</c>      — true when no real answers exist (cold-start path).
/// </summary>
public record AdaptivitySignals(
    double AccuracyPct,
    double AvgTimeSeconds,
    double HintRate,
    int    RetryCount,
    int    MasteryPct,
    bool   IsDefault);

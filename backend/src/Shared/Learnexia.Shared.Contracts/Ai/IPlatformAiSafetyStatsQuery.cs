namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// Cross-module platform-aggregate read seam: Ai implements, façade consumers inject.
/// Returns platform-wide AI safety event statistics over a UTC time window.
///
/// <para>Registered in the Ai module's infrastructure DI as Scoped.</para>
///
/// <para>P7-10 analytics dashboard (option a — honest v1).
/// Safety event aggregates (<c>SafetyEvent</c> table) are REAL data available now.
/// AI request volume is now ALSO real — sourced from the <c>ai.AiUsageLogs</c> table (built in
/// the P7-11 tutor-cost sub-batch). See <see cref="PlatformAiSafetyStats.AiRequestVolume"/>.
/// (Note: streamed AI responses are not yet captured in <c>AiUsageLogs</c> — a documented v1 gap —
/// so the volume counts non-streaming completions.)</para>
///
/// <para>All results are sentinel-safe: an empty window returns zeroed stats — never null, never throws.</para>
/// </summary>
public interface IPlatformAiSafetyStatsQuery
{
    /// <summary>
    /// Returns platform-wide AI safety stats over [<paramref name="fromUtc"/>, <paramref name="toUtc"/>).
    /// </summary>
    /// <param name="fromUtc">Window start (inclusive), UTC.</param>
    /// <param name="toUtc">Window end (exclusive), UTC.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="PlatformAiSafetyStats"/> value — never null.
    /// Returns a zeroed instance when no safety events exist in the window.
    /// </returns>
    Task<PlatformAiSafetyStats> GetPlatformAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default);
}

/// <summary>
/// Platform-wide AI safety statistics over a UTC time window.
/// Produced by <see cref="IPlatformAiSafetyStatsQuery"/>.
/// </summary>
/// <param name="TotalSafetyEvents">
/// Total <c>SafetyEvent</c> rows in the window (all actions: blocked, regenerated, fallback).
/// </param>
/// <param name="BlockedCount">
/// Count of <c>SafetyEvent</c> rows where <c>ActionTaken = "Blocked"</c>.
/// </param>
/// <param name="FlaggedCount">
/// Count of <c>SafetyEvent</c> rows where <c>ActionTaken</c> is not "Blocked"
/// (i.e. "Regenerated" or "FallbackReturned" — flagged but not hard-blocked).
/// </param>
/// <param name="AiRequestVolume">
/// Total AI tutor requests in the window — count of <c>ai.AiUsageLogs</c> rows. Real data
/// (the AiUsageLogs table is built). Counts non-streaming completions; streamed responses are
/// a documented v1 capture gap. Zero when no calls occurred (a real answer, not N/A).
/// </param>
/// <param name="AiRequestVolumeNaReason">
/// Explicit N/A marker for AI request volume — non-null only if the volume cannot be derived.
/// Now that <c>ai.AiUsageLogs</c> exists this is <c>null</c> (data available); kept on the contract
/// so consumers can still distinguish "0 requests" from "data unavailable".
/// </param>
public record PlatformAiSafetyStats(
    int TotalSafetyEvents,
    int BlockedCount,
    int FlaggedCount,
    int AiRequestVolume,
    string? AiRequestVolumeNaReason);

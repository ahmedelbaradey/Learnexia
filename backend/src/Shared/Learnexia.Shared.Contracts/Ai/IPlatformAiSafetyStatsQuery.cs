namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// Cross-module platform-aggregate read seam: Ai implements, façade consumers inject.
/// Returns platform-wide AI safety event statistics over a UTC time window.
///
/// <para>Registered in the Ai module's infrastructure DI as Scoped.</para>
///
/// <para>P7-10 analytics dashboard (option a — honest v1).
/// Safety event aggregates (<c>SafetyEvent</c> table) are REAL data available now.
/// AI request volume is NOT available — the <c>AiUsageLogs</c> table does not yet exist
/// (it belongs to the P7-11 cost sub-batch). See <see cref="PlatformAiSafetyStats.AiRequestVolumeNaReason"/>
/// for the explicit N/A marker.</para>
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
/// <param name="AiRequestVolumeNaReason">
/// Explicit N/A marker for AI request volume — set to a non-null string when request volume
/// cannot be derived (e.g. "N/A (AiUsageLogs not yet built — held P7-11 cost sub-batch)").
/// Null means request-volume data is available (reserved for when P7-11 AiUsageLogs lands).
/// </param>
public record PlatformAiSafetyStats(
    int TotalSafetyEvents,
    int BlockedCount,
    int FlaggedCount,
    string? AiRequestVolumeNaReason);

namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Cross-module platform-aggregate read seam: Gamification implements, façade consumers inject.
/// Returns platform-wide engagement statistics over a UTC time window.
///
/// <para>Registered in <c>AddGamificationInfrastructure()</c> as Scoped.</para>
///
/// <para>P7-10 analytics dashboard (option a — honest v1).
/// All results are sentinel-safe: an empty window returns zeroed stats — never null, never throws.</para>
///
/// <para>OQ-1 (brief): Gamification persists an <c>XpAward</c> append-only ledger (<c>OccurredAtUtc</c>
/// is the event timestamp). Therefore XP-earned-in-window IS derivable by summing
/// <c>XpAward.XpAmount WHERE OccurredAtUtc ∈ [from, to)</c>.</para>
/// </summary>
public interface IPlatformEngagementQuery
{
    /// <summary>
    /// Returns platform-wide engagement stats over [<paramref name="fromUtc"/>, <paramref name="toUtc"/>).
    /// </summary>
    /// <param name="fromUtc">Window start (inclusive), UTC.</param>
    /// <param name="toUtc">Window end (exclusive), UTC.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="PlatformEngagementStats"/> value — never null.
    /// Returns a zeroed instance when no data exists in the window.
    /// </returns>
    Task<PlatformEngagementStats> GetPlatformAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default);
}

/// <summary>
/// Platform-wide engagement statistics over a UTC time window.
/// Produced by <see cref="IPlatformEngagementQuery"/>.
/// </summary>
/// <param name="MissionsCompleted">
/// Count of <c>StudentMission</c> rows that transitioned to <c>Completed</c> in the window
/// (using <c>CompletedAtUtc</c>).
/// </param>
/// <param name="XpEarnedInWindow">
/// Sum of <c>XpAward.XpAmount</c> for all awards whose <c>OccurredAtUtc</c> falls in [from, to).
/// Sourced from the append-only <c>XpAward</c> ledger.
/// </param>
public record PlatformEngagementStats(
    int MissionsCompleted,
    long XpEarnedInWindow);

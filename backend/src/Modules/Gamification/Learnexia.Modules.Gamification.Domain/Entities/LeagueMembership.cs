using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Gamification.Domain.Entities;

/// <summary>
/// Per-student per-week league membership row. One row per (StudentXpProfileId, PeriodKey) —
/// enforced by DB unique constraint <c>UX_LeagueMemberships_PeriodKey_StudentXpProfileId</c>.
///
/// A second unique constraint <c>UX_LeagueMemberships_LeagueId_StudentXpProfileId</c> acts as a
/// backstop for concurrent placement races.
///
/// <see cref="WeeklyXp"/> is mutable via <see cref="AddWeeklyXp"/> — incremented by
/// <c>XpAwardedLeagueHandler</c> through <c>IncrementLeagueXpCommand</c> on every XP event.
///
/// <see cref="FinalRank"/>, <see cref="TierAfter"/>, <see cref="Status"/> are null/Active until
/// <c>LeaguePromotionJob</c> stamps them on Monday rollover.
///
/// <see cref="JoinOrder"/> is stable per-cohort (1..N, assigned at insert time) and drives the
/// "Student #N" anonymization display (D2).
///
/// Derives from <see cref="CreationAuditedEntity"/> — the row represents the join event; no
/// admin edits are planned except via the rollover job (which uses <c>internal set</c> fields).
/// </summary>
public sealed class LeagueMembership : CreationAuditedEntity
{
    /// <summary>FK to <see cref="League"/> — RESTRICT delete (preserve history).</summary>
    public int LeagueId { get; private set; }

    /// <summary>Navigation — resolved by EF at SaveChanges time.</summary>
    public League League { get; private set; } = null!;

    /// <summary>FK to <see cref="StudentXpProfile"/> — CASCADE delete.</summary>
    public int StudentXpProfileId { get; private set; }

    /// <summary>Navigation — resolved by EF at SaveChanges time.</summary>
    public StudentXpProfile StudentXpProfile { get; private set; } = null!;

    /// <summary>
    /// Denormalized from <see cref="League.PeriodKey"/> at insert time.
    /// Used by the unique constraint <c>UX_LeagueMemberships_PeriodKey_StudentXpProfileId</c>
    /// to guarantee one membership per student per week without a JOIN to Leagues.
    /// </summary>
    public string PeriodKey { get; private set; } = null!;

    /// <summary>
    /// 1-based stable per-cohort ordinal. Assigned at join time as COUNT(existing) + 1.
    /// Drives anonymized display name "Student #N" (D2). Also used as tiebreak in standings
    /// queries alongside <see cref="JoinedAtUtc"/>.
    /// </summary>
    public int JoinOrder { get; private set; }

    /// <summary>
    /// UTC timestamp when this membership was instantiated (first dashboard load of the week).
    /// Secondary sort key for standings tiebreak: <c>WeeklyXp DESC, JoinedAtUtc ASC</c> (D4).
    /// </summary>
    public DateTime JoinedAtUtc { get; private set; }

    /// <summary>
    /// XP earned within this week's competition period. Starts at 0.
    /// Mutated by <c>IncrementLeagueXpCommand</c> via <see cref="AddWeeklyXp"/>.
    /// </summary>
    public int WeeklyXp { get; internal set; }

    /// <summary>
    /// Final rank within the cohort. Null until <c>LeaguePromotionJob</c> stamps it on rollover.
    /// </summary>
    public int? FinalRank { get; internal set; }

    /// <summary>
    /// Tier the student moves to after rollover. Null until job stamps it.
    /// Stored as int via EF HasConversion.
    /// </summary>
    public LeagueTier? TierAfter { get; internal set; }

    /// <summary>
    /// Lifecycle status. Active during the week; Promoted/Demoted/Stayed after rollover.
    /// </summary>
    public MembershipStatus Status { get; internal set; } = MembershipStatus.Active;

    // ---------------------------------------------------------------------------
    // EF constructor
    // ---------------------------------------------------------------------------

    private LeagueMembership() { }

    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Creates a new membership row. Passes nav references so EF Core resolves real FKs at
    /// SaveChanges time (graph-navigation convention, fifth instance per HANDOFF note).
    /// </summary>
    public static LeagueMembership Create(
        League league,
        StudentXpProfile profile,
        string periodKey,
        int joinOrder,
        DateTime joinedAtUtc)
        => new()
        {
            League = league,
            StudentXpProfile = profile,
            PeriodKey = periodKey,
            JoinOrder = joinOrder,
            JoinedAtUtc = joinedAtUtc,
            WeeklyXp = 0,
            Status = MembershipStatus.Active,
        };

    // ---------------------------------------------------------------------------
    // Mutation
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Increments <see cref="WeeklyXp"/> by <paramref name="delta"/>. Clamped at int.MaxValue.
    /// Called by <c>IncrementLeagueXpCommand</c> handler.
    /// </summary>
    public void AddWeeklyXp(int delta) => WeeklyXp = (int)Math.Min((long)WeeklyXp + delta, int.MaxValue);

    /// <summary>
    /// Stamps the post-rollover fields on this membership. Called exclusively by
    /// <c>LeagueRolloverJob</c> during the Monday 00:15 UTC promotion sweep (P4-07, AC2).
    /// Idempotent guard: if <see cref="TierAfter"/> is already set, this is a no-op
    /// (the job skips at cohort level, but method-level guard is defensive-in-depth).
    /// </summary>
    public void FinalizePromotion(int finalRank, LeagueTier tierAfter, MembershipStatus status)
    {
        if (TierAfter != null) return;   // idempotent guard
        FinalRank = finalRank;
        TierAfter = tierAfter;
        Status    = status;
    }
}

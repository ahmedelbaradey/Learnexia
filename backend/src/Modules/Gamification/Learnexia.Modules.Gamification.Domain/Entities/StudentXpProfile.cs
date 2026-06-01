using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Shared.Kernel.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace Learnexia.Modules.Gamification.Domain.Entities;

/// <summary>
/// Per-student XP snapshot. One row per student, created lazily on the first XP award.
/// Keyed by <see cref="StudentId"/> (plain int — no cross-module FK to Identity). Unique
/// index <c>UX_StudentXpProfiles_StudentId</c> enforces the one-per-student invariant at the
/// DB layer and acts as the read path index.
///
/// Derives from <see cref="AggregateRoot"/> so that <see cref="StudentLeveledUpDomainEvent"/>
/// can be raised and dispatched by <c>UnitOfWorkBehavior</c> strictly AFTER a successful commit.
/// </summary>
public class StudentXpProfile : AggregateRoot
{
    /// <summary>Cross-module soft reference to the Identity student. No FK constraint.</summary>
    public int StudentId { get; set; }

    /// <summary>Cumulative XP earned — never decreases in P4-02.</summary>
    public int TotalXp { get; set; }

    /// <summary>Current level derived from <c>LevelCurve.LevelFor(TotalXp)</c>. Starts at 1.</summary>
    public int CurrentLevel { get; set; } = 1;

    /// <summary>UTC timestamp of the last XpAward that updated this profile.</summary>
    public DateTime LastAwardAtUtc { get; set; }

    /// <summary>Navigation property — all XP award ledger rows for this student.</summary>
    public ICollection<XpAward> Awards { get; set; } = new List<XpAward>();

    // ---------------------------------------------------------------------------
    // Streak fields (added P4-03-B1-1)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Current streak length — consecutive days with qualifying lesson activity. 0 = no active streak.
    /// Mutated only via <see cref="AdvanceStreak"/>, <see cref="ResetStreakAndStart"/>, and
    /// <see cref="BreakStreak"/>. Internal setter enforces that invariant within the module.
    /// </summary>
    public int CurrentStreak { get; internal set; } = 0;

    /// <summary>
    /// All-time longest streak. Monotonic — never decreases. Preserved when streak breaks.
    /// Mutated only via <see cref="AdvanceStreak"/> and <see cref="ResetStreakAndStart"/>.
    /// </summary>
    public int LongestStreak { get; internal set; } = 0;

    /// <summary>
    /// The activity date (UTC, day-only) of the last streak-advancing lesson completion.
    /// Null for brand-new students who have never completed a lesson.
    /// Stored as PG <c>date</c> (DateOnly → Npgsql 8+ maps natively).
    /// Mutated only via <see cref="AdvanceStreak"/> and <see cref="ResetStreakAndStart"/>.
    /// </summary>
    public DateOnly? LastActivityDateUtc { get; internal set; }

    // ---------------------------------------------------------------------------
    // Hearts fields (added P4-04-B1-1)
    // ---------------------------------------------------------------------------

    /// <summary>Current hearts count [0..Cap]. Defaults to Cap (5) for new students.</summary>
    public int Hearts { get; private set; } = 5;

    /// <summary>
    /// UTC timestamp when the last heart refill tick was applied.
    /// Null = clock not running (brand-new student OR all hearts full and idle).
    /// Reset to OccurredAtUtc on each LoseHeart call (Duolingo clock-reset semantics).
    /// </summary>
    public DateTime? LastHeartRefillAtUtc { get; private set; }

    // ---------------------------------------------------------------------------
    // Hearts computed property (P4-04-B2a-3)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Derived from <see cref="Hearts"/> — true when <c>Hearts == 0</c>.
    /// Reward paths use this to short-circuit: no XP, no streak advance, no further heart loss.
    /// <c>[NotMapped]</c> — not a stored column; EF must not attempt to map it.
    /// </summary>
    [NotMapped]
    public bool InPracticeMode => Hearts == 0;

    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    /// <summary>Creates a brand-new profile for a student who has never earned XP.</summary>
    public static StudentXpProfile CreateFor(int studentId)
        => new()
        {
            StudentId = studentId,
            TotalXp = 0,
            CurrentLevel = 1,
            LastAwardAtUtc = DateTime.UtcNow,
            // Hearts default = 5 (cap constant). Literal here because Domain cannot reference
            // Application.GamificationConstants (layer boundary). The EF column default is also 5
            // (set in StudentXpProfileConfig). Runtime cap is enforced by HeartsOptions.Cap.
            Hearts = 5,
            LastHeartRefillAtUtc = null,   // clock idle — no loss yet
        };

    // ---------------------------------------------------------------------------
    // Mutation
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Applies an XP award to the profile. Raises <see cref="StudentLeveledUpDomainEvent"/>
    /// when <paramref name="newLevel"/> exceeds the current level. Both the XP increment and
    /// the domain-event raise happen in memory; the <c>UnitOfWorkBehavior</c> commits and
    /// then dispatches the domain event after commit.
    /// </summary>
    public void ApplyAward(int amount, int newLevel)
    {
        var oldLevel = CurrentLevel;
        TotalXp += amount;
        CurrentLevel = newLevel;
        LastAwardAtUtc = DateTime.UtcNow;

        if (newLevel > oldLevel)
            RaiseDomainEvent(new StudentLeveledUpDomainEvent(StudentId, oldLevel, newLevel));
    }

    // ---------------------------------------------------------------------------
    // Streak mutation (P4-03-B2-2)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Advances the streak by 1 for <paramref name="activityDate"/>. Updates
    /// <see cref="LongestStreak"/> if the new value exceeds the prior record.
    /// Raises <see cref="StreakAdvancedDomainEvent"/>.
    ///
    /// Call only when the handler has confirmed <paramref name="activityDate"/> is strictly
    /// the next consecutive day after <see cref="LastActivityDateUtc"/> (or the first-ever activity).
    /// Does NOT award XP — the <c>AdvanceStreakCommandHandler</c> does that separately.
    /// </summary>
    public void AdvanceStreak(DateOnly activityDate)
    {
        CurrentStreak = (LastActivityDateUtc is null || CurrentStreak == 0) ? 1 : CurrentStreak + 1;
        LastActivityDateUtc = activityDate;
        LongestStreak = Math.Max(LongestStreak, CurrentStreak);
        RaiseDomainEvent(new StreakAdvancedDomainEvent(StudentId, CurrentStreak, LongestStreak, activityDate));
    }

    /// <summary>
    /// Resets the streak to 1 (the new activity is day-1 of a fresh streak) after a gap &gt; 1 day.
    /// <see cref="LongestStreak"/> is preserved — it never decreases.
    /// Raises <see cref="StreakBrokenDomainEvent"/> for the old streak, then
    /// <see cref="StreakAdvancedDomainEvent"/> for the new day-1.
    /// Does NOT award XP — the <c>AdvanceStreakCommandHandler</c> does that separately.
    /// </summary>
    public void ResetStreakAndStart(DateOnly activityDate)
    {
        var previousStreak = CurrentStreak;
        CurrentStreak = 1;
        LastActivityDateUtc = activityDate;
        LongestStreak = Math.Max(LongestStreak, CurrentStreak);
        RaiseDomainEvent(new StreakBrokenDomainEvent(StudentId, previousStreak, activityDate));
        RaiseDomainEvent(new StreakAdvancedDomainEvent(StudentId, CurrentStreak, LongestStreak, activityDate));
    }

    /// <summary>
    /// Called by the sweep job to flag a broken streak when the student went silent.
    /// Sets <see cref="CurrentStreak"/> = 0; does NOT touch <see cref="LongestStreak"/> or
    /// <see cref="LastActivityDateUtc"/>.
    /// Raises <see cref="StreakBrokenDomainEvent"/>.
    /// Idempotent — no-op when <see cref="CurrentStreak"/> is already 0.
    /// </summary>
    public void BreakStreak()
    {
        if (CurrentStreak == 0) return;

        var previousStreak = CurrentStreak;
        CurrentStreak = 0;
        RaiseDomainEvent(new StreakBrokenDomainEvent(StudentId, previousStreak, LastActivityDateUtc ?? DateOnly.MinValue));
    }

    // ---------------------------------------------------------------------------
    // Hearts mutation (P4-04-B2a-3)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Applies lazy time-based refill via <see cref="LazyHeartRefiller.Compute"/> then decrements
    /// <see cref="Hearts"/> by 1 (floor 0, Duolingo-style). Updates <see cref="LastHeartRefillAtUtc"/>
    /// to <paramref name="occurredAtUtc"/> (the refill clock RESETS on each loss — D13).
    ///
    /// Raises <see cref="HeartsDepletedDomainEvent"/> when <see cref="Hearts"/> transitions to 0.
    /// When already at 0 the decrement is a no-op (Math.Max 0); the handler still writes the
    /// HeartLoss audit row (D7) — the wrong answer happened; the audit reflects it.
    /// </summary>
    public void LoseHeart(DateTime occurredAtUtc, int cap, int intervalMinutes)
    {
        // Lazy-refill FIRST: if enough time has elapsed, bring hearts up to date
        // before deciding whether to decrement. A student idle for 60+ min sees
        // full hearts restored, then loses one — not permanently stuck at 0 (D5).
        var (refilled, newLastRefill, _) = LazyHeartRefiller.Compute(
            Hearts, cap, LastHeartRefillAtUtc, occurredAtUtc, intervalMinutes);

        Hearts = refilled;
        LastHeartRefillAtUtc = newLastRefill;

        // Decrement (clamped at 0).
        var wasInPracticeMode = InPracticeMode;
        Hearts = Math.Max(0, Hearts - 1);

        // Reset the refill clock to this loss — next refill is exactly intervalMinutes from now.
        LastHeartRefillAtUtc = occurredAtUtc;

        // Raise depletion event only on the TRANSITION (4→0, not 0→0).
        if (!wasInPracticeMode && InPracticeMode)
            RaiseDomainEvent(new HeartsDepletedDomainEvent(StudentId, occurredAtUtc));
    }

    /// <summary>
    /// Adds +1 heart (capped at <paramref name="cap"/>). Called on a correct answer while in
    /// Practice Mode when <c>RegenOnCorrectAnswerInPracticeMode = true</c> (D2 / Q3 = yes).
    ///
    /// Raises <see cref="HeartsRefilledDomainEvent"/> when <see cref="Hearts"/> transitions out of 0.
    /// No-op when already at cap.
    /// </summary>
    public void RegainHeartFromPractice(int cap)
    {
        if (Hearts >= cap) return;

        var before = Hearts;
        Hearts += 1;

        // If now at cap, clear the refill clock — clock idles when full.
        if (Hearts == cap)
            LastHeartRefillAtUtc = null;

        // Raise event when Hearts transitions from 0 → 1 (student exits Practice Mode).
        if (before == 0)
            RaiseDomainEvent(new HeartsRefilledDomainEvent(StudentId, Hearts, Source: "practice"));
    }

    /// <summary>
    /// Applies lazy time-based refill bookkeeping. Used by reward command handlers to ensure
    /// the Practice Mode check uses a fresh hearts value before short-circuiting.
    ///
    /// Does NOT raise <see cref="HeartsRefilledDomainEvent"/> — this is bookkeeping only
    /// (no business-meaningful transition from the student's POV). Returns the count regenerated.
    /// </summary>
    public int RefreshHeartsAgainst(DateTime nowUtc, int cap, int intervalMinutes)
    {
        var result = LazyHeartRefiller.Compute(Hearts, cap, LastHeartRefillAtUtc, nowUtc, intervalMinutes);

        if (result.Regenerated > 0)
        {
            Hearts = result.NewHearts;
            LastHeartRefillAtUtc = result.NewLastRefillAt;
        }

        return result.Regenerated;
    }

    // ---------------------------------------------------------------------------
    // Badge mutation (P4-05-B3-1)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Records that this student earned a badge. Adds the XP bonus to TotalXp, recomputes level,
    /// and raises <see cref="BadgeEarnedDomainEvent"/> + <see cref="StudentLeveledUpDomainEvent"/>
    /// if the bonus pushes them past a level threshold. Does NOT write the StudentBadge ledger row —
    /// the handler does that explicitly.
    ///
    /// Domain-event chain implication: if the badge XP bonus levels the student up,
    /// <see cref="StudentLeveledUpDomainEvent"/> is raised. That event will trigger
    /// <c>StudentLeveledUpBadgeHandler</c>, which could award further LEVEL_* badges.
    /// This is correct chain semantics. The <c>alreadyEarned</c> set inside the predicate
    /// evaluator prevents infinite chains since each badge awards at most once.
    ///
    /// Practice Mode by-construction: badge awards are NOT gated by Practice Mode. A student
    /// in Practice Mode who earns a badge via <c>LessonCompletedIntegrationEvent</c> still
    /// receives the badge and its XP bonus. <c>StreakAdvancedDomainEvent</c> and
    /// <c>StudentLeveledUpDomainEvent</c> are never raised in Practice Mode (upstream handlers
    /// short-circuit), so STREAK_* and LEVEL_* badges cannot fire in Practice Mode by construction.
    /// </summary>
    public void RecordBadgeEarned(
        int badgeDefinitionId,
        string code,
        BadgeRarity rarity,
        int rewardXp,
        int newLevel,
        DateTime awardedAtUtc)
    {
        var oldLevel = CurrentLevel;
        TotalXp += rewardXp;
        CurrentLevel = newLevel;
        LastAwardAtUtc = awardedAtUtc;

        if (newLevel > oldLevel)
            RaiseDomainEvent(new StudentLeveledUpDomainEvent(StudentId, oldLevel, newLevel));

        RaiseDomainEvent(new BadgeEarnedDomainEvent(
            StudentId,
            badgeDefinitionId,
            code,
            rarity,
            rewardXp,
            awardedAtUtc));
    }

    // ---------------------------------------------------------------------------
    // Mission mutation (P4-06-B2-1)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Records that this student completed a mission. Adds the reward XP to TotalXp, recomputes
    /// level, and raises <see cref="Events.MissionCompletedDomainEvent"/> + optionally
    /// <see cref="StudentLeveledUpDomainEvent"/> if the XP bonus crosses a level threshold.
    ///
    /// Does NOT mutate the <c>StudentMission</c> row — the command handler does that explicitly
    /// before calling this method.
    ///
    /// Mirrors <see cref="RecordBadgeEarned"/> line-for-line (same XP-award + level-up + event
    /// raise pattern; different domain event type).
    /// </summary>
    public void RecordMissionCompleted(
        int missionDefinitionId,
        string code,
        int rewardXp,
        int newLevel,
        DateTime completedAtUtc)
    {
        var oldLevel = CurrentLevel;
        TotalXp += rewardXp;
        CurrentLevel = newLevel;
        LastAwardAtUtc = completedAtUtc;

        if (newLevel > oldLevel)
            RaiseDomainEvent(new StudentLeveledUpDomainEvent(StudentId, oldLevel, newLevel));

        RaiseDomainEvent(new Events.MissionCompletedDomainEvent(
            StudentId,
            missionDefinitionId,
            code,
            rewardXp,
            completedAtUtc));
    }
}

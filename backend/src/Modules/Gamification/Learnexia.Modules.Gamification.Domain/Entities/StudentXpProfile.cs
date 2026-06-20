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
    // Streak freeze fields (added P4-11-B0; methods added P4-11-B1-A)
    // ---------------------------------------------------------------------------

    /// <summary>Maximum streak freezes a student can hold at one time.</summary>
    public const int MaxFreezes = 2;

    /// <summary>
    /// Current number of streak freezes the student holds. Range [0 .. MaxFreezes].
    /// Cap enforced at application layer via <c>FreezeOptions.MaxInventory</c> and at the DB
    /// layer via a CHECK constraint. Defaults to 0 for all students — no opening grant (Q6 lock).
    ///
    /// Mutated only via <see cref="GrantFreeze"/> and <see cref="ConsumeFreeze"/>.
    /// Private setter enforces that invariant within the module.
    /// </summary>
    public int FreezeBalance { get; private set; } = 0;

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
            FreezeBalance = 0,             // brand-new students start with zero freezes (Q6 lock)
        };

    // ---------------------------------------------------------------------------
    // Mutation
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Single chokepoint for all XP additions. Raises <see cref="XpAwardedDomainEvent"/> on every
    /// call (consumed by <c>XpAwardedLeagueHandler</c> in P4-07) and raises
    /// <see cref="StudentLeveledUpDomainEvent"/> when <paramref name="newLevel"/> exceeds the
    /// current level. Both increments and domain-event raises happen in memory; the
    /// <c>UnitOfWorkBehavior</c> commits and then dispatches the events after commit (ADR 0002).
    ///
    /// <paramref name="occurredAtUtc"/> is the event timestamp (not wall-clock at handling time),
    /// which is stored in <see cref="LastAwardAtUtc"/> — reflects when the underlying action happened.
    /// </summary>
    public void ApplyAward(int amount, int newLevel, XpReason reason, DateTime occurredAtUtc)
    {
        var oldLevel = CurrentLevel;
        TotalXp += amount;
        CurrentLevel = newLevel;
        LastAwardAtUtc = occurredAtUtc;

        RaiseDomainEvent(new XpAwardedDomainEvent(StudentId, amount, TotalXp, reason, occurredAtUtc));

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
    // Streak freeze mutation (P4-11-B1-A)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Earns 1 freeze (called on a 7-day streak milestone). No-op when already at
    /// <see cref="MaxFreezes"/> — no event raised on a no-op (cap-silent rule).
    ///
    /// Raises <see cref="StreakFreezeGrantedDomainEvent"/> so the streak cache is invalidated
    /// immediately after commit — fixes P4-11 audit Medium #2 (stale FreezeBalance on dashboard).
    /// </summary>
    public void GrantFreeze(DateTime occurredAtUtc)
    {
        if (FreezeBalance >= MaxFreezes) return;   // no-op at cap — no event raised
        FreezeBalance += 1;
        RaiseDomainEvent(new StreakFreezeGrantedDomainEvent(StudentId, FreezeBalance, occurredAtUtc));
    }

    /// <summary>
    /// P7-13 admin grant: adds up to <paramref name="count"/> freezes, clamped at
    /// <see cref="MaxFreezes"/>. Raises <see cref="StreakFreezeGrantedDomainEvent"/> once for the
    /// final balance (or not at all if already at cap). Returns the actual number granted.
    /// </summary>
    public int GrantFreezes(int count, DateTime occurredAtUtc)
    {
        if (count <= 0) return 0;
        if (FreezeBalance >= MaxFreezes) return 0;

        int canGrant = MaxFreezes - FreezeBalance;
        int actualGrant = Math.Min(count, canGrant);
        FreezeBalance += actualGrant;

        RaiseDomainEvent(new StreakFreezeGrantedDomainEvent(StudentId, FreezeBalance, occurredAtUtc));
        return actualGrant;
    }

    /// <summary>
    /// Consumes 1 freeze. Raises <see cref="StreakFreezeConsumedDomainEvent"/> on success.
    /// Throws <see cref="InvalidOperationException"/> when <see cref="FreezeBalance"/> is 0.
    /// Callers MUST check <c>FreezeBalance &gt; 0</c> before calling (the StreakSweepJob's
    /// freeze branch does this — Batch 3-A scope).
    /// </summary>
    public void ConsumeFreeze()
    {
        if (FreezeBalance <= 0)
            throw new InvalidOperationException("Cannot consume freeze when balance is 0.");

        FreezeBalance -= 1;
        RaiseDomainEvent(new StreakFreezeConsumedDomainEvent(
            StudentId,
            CurrentStreak,
            FreezeBalance,
            DateTime.UtcNow));
    }

    /// <summary>
    /// Called by <c>StreakSweepJob</c> (Pass 2) to consume 1 freeze and shift
    /// <see cref="LastActivityDateUtc"/> forward by 1 day so the streak appears maintained.
    /// Uses the sweep job's clock timestamp rather than <c>DateTime.UtcNow</c>, preserving
    /// determinism across time zones and test scenarios.
    ///
    /// Raises <see cref="StreakFreezeConsumedDomainEvent"/> with the caller-supplied
    /// <paramref name="occurredAtUtc"/>. Does NOT call <see cref="ConsumeFreeze"/> to avoid
    /// raising a duplicate event.
    ///
    /// Precondition: <see cref="FreezeBalance"/> &gt; 0 — callers MUST check before calling.
    /// Throws <see cref="InvalidOperationException"/> when balance is 0.
    /// </summary>
    public void ConsumeFreezeAndShiftActivity(DateOnly newLastActivityDate, DateTime occurredAtUtc)
    {
        if (FreezeBalance <= 0)
            throw new InvalidOperationException("Cannot consume freeze when balance is 0.");

        FreezeBalance -= 1;
        LastActivityDateUtc = newLastActivityDate;

        RaiseDomainEvent(new StreakFreezeConsumedDomainEvent(
            StudentId,
            CurrentStreak,
            FreezeBalance,
            occurredAtUtc));
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
    // Timed-event participation mutation (P4-12)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Records that this student completed a timed-event participation. Adds the reward XP to
    /// TotalXp, recomputes level, and raises <see cref="Events.TimedEventParticipationCompletedDomainEvent"/>
    /// + optionally <see cref="StudentLeveledUpDomainEvent"/> if the XP bonus crosses a level threshold.
    ///
    /// Does NOT mutate the <c>TimedEventParticipation</c> row — the <c>XpService</c> accrual hook
    /// does that explicitly before calling this method.
    ///
    /// Mirrors <see cref="RecordMissionCompleted"/> line-for-line (same XP-award + level-up +
    /// event-raise pattern; different domain event type). The <see cref="XpReason.TimedEventCompleted"/>
    /// value distinguishes the ledger row in analytics.
    /// </summary>
    public void RecordTimedEventCompleted(
        int timedEventId,
        string code,
        int rewardXp,
        int newLevel,
        DateTime completedAtUtc)
    {
        // Delegate XP mutation + XpAwardedDomainEvent raise + optional level-up event to the
        // single chokepoint so that XpAwardedLeagueHandler (P4-07) sees timed-event completion XP too.
        ApplyAward(rewardXp, newLevel, XpReason.TimedEventCompleted, completedAtUtc);

        // Timed-event-specific domain event raised AFTER ApplyAward.
        RaiseDomainEvent(new Events.TimedEventParticipationCompletedDomainEvent(
            StudentId,
            timedEventId,
            code,
            rewardXp,
            completedAtUtc));
    }

    // ---------------------------------------------------------------------------
    // League fields (P4-07-B1-1)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Current league tier. Defaults Bronze (brand-new students start Bronze per FR-GM-6).
    /// Updated by LeaguePromotionJob on Monday rollover via <see cref="UpdateTier"/>.
    /// Read by LeaguePlacementService to determine which tier's cohort to place this student in.
    /// </summary>
    public LeagueTier CurrentTier { get; private set; } = LeagueTier.Bronze;

    /// <summary>
    /// Updates the student's current league tier. Called by LeagueRolloverJob on promotion/demotion.
    /// No domain event raised in this batch — forward-compat for P4-08 motion if needed.
    /// </summary>
    public void UpdateTier(LeagueTier newTier)
    {
        CurrentTier = newTier;
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
        // Delegate XP mutation + XpAwardedDomainEvent raise + optional level-up event to the
        // single chokepoint so that XpAwardedLeagueHandler (P4-07) sees badge XP too.
        ApplyAward(rewardXp, newLevel, XpReason.BadgeEarned, awardedAtUtc);

        // Badge-specific event raised AFTER ApplyAward (XpAwardedDomainEvent dispatched first).
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
        // Delegate XP mutation + XpAwardedDomainEvent raise + optional level-up event to the
        // single chokepoint so that XpAwardedLeagueHandler (P4-07) sees mission XP too.
        ApplyAward(rewardXp, newLevel, XpReason.MissionCompleted, completedAtUtc);

        // Mission-specific event raised AFTER ApplyAward.
        RaiseDomainEvent(new Events.MissionCompletedDomainEvent(
            StudentId,
            missionDefinitionId,
            code,
            rewardXp,
            completedAtUtc));
    }
}

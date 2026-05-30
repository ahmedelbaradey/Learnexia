using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Shared.Kernel.Entities;

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
}

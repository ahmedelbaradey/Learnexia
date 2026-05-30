using Learnexia.Modules.Gamification.Domain.Events;
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
}

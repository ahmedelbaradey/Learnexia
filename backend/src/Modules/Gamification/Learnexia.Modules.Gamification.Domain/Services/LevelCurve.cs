namespace Learnexia.Modules.Gamification.Domain.Services;

/// <summary>
/// Pure static XP-to-level mapping for the Gamification module (FR-GM-1, Q3 lead-decision).
///
/// Table-based for L1–L10 (kid-friendly early-game: L2 in ~10 correct answers), linear extension
/// at +5000 XP per level for L11+. No DI, no DB access — fully unit-testable in isolation.
///
/// Mirrors the shape of <c>LearningPathEngine</c> (pure static service in the Domain layer).
///
/// Thread-safe: all methods are pure functions over the immutable <see cref="Thresholds"/> array.
/// </summary>
public static class LevelCurve
{
    // Cumulative XP thresholds for levels 1–10.
    // Thresholds[i] = XP required to REACH level (i+1), i.e. Thresholds[0] = 0 → Level 1 starts at 0 XP.
    private static readonly int[] Thresholds = { 0, 100, 250, 500, 1000, 2000, 4000, 7000, 11000, 16000 };

    /// <summary>
    /// Returns the 1-based level for a given cumulative XP total.
    /// <paramref name="totalXp"/> must be ≥ 0; throws <see cref="ArgumentOutOfRangeException"/> otherwise.
    /// </summary>
    public static int LevelForXp(int totalXp)
    {
        if (totalXp < 0)
            throw new ArgumentOutOfRangeException(nameof(totalXp), "XP total cannot be negative.");

        // Walk the table from the top down for efficiency on high-XP students.
        for (int i = Thresholds.Length - 1; i >= 0; i--)
        {
            if (totalXp >= Thresholds[i])
            {
                int level = i + 1; // Thresholds[i] is the threshold for level (i+1)
                if (i == Thresholds.Length - 1 && totalXp > Thresholds[i])
                {
                    // L11+ extension: each additional level requires 5000 more XP.
                    level += (totalXp - Thresholds[i]) / 5000;
                }
                return level;
            }
        }

        return 1; // Unreachable for valid input, but satisfies the compiler.
    }

    /// <summary>
    /// Returns the cumulative XP required to reach a given level (1-based).
    /// <paramref name="level"/> must be ≥ 1; throws <see cref="ArgumentOutOfRangeException"/> otherwise.
    /// </summary>
    public static int XpForLevel(int level)
    {
        if (level < 1)
            throw new ArgumentOutOfRangeException(nameof(level), "Level must be at least 1.");

        if (level <= Thresholds.Length)
            return Thresholds[level - 1];

        // L11+: 16000 + (level - 10) * 5000
        return Thresholds[^1] + (level - Thresholds.Length) * 5000;
    }

    /// <summary>
    /// Returns a tuple of the current level, XP earned within the current level, and XP
    /// <em>remaining</em> to reach the next level. Useful for rendering XP progress bars.
    ///
    /// Semantics:
    ///   <c>XpIntoLevel</c> = <c>totalXp - XpForLevel(level)</c> (XP earned since the level floor).
    ///   <c>XpToNext</c>    = <c>XpForLevel(level + 1) - totalXp</c> (remaining XP to next level).
    ///
    /// Examples:
    ///   Compute(0)     → (1,  0, 100)   [need 100 more XP to reach L2]
    ///   Compute(150)   → (2, 50, 100)   [250 − 150 = 100 remaining to L3]
    ///   Compute(16000) → (10, 0, 5000)  [21000 − 16000 = 5000 remaining to L11]
    /// </summary>
    public static (int Level, int XpIntoLevel, int XpToNext) Compute(int totalXp)
    {
        var level = LevelForXp(totalXp);
        var currentLevelFloor = XpForLevel(level);
        var nextLevelFloor = XpForLevel(level + 1);

        return (level, totalXp - currentLevelFloor, nextLevelFloor - totalXp);
    }
}

namespace Learnexia.Modules.Gamification.Application.Configuration;

/// <summary>
/// Configuration options for the streak-freeze mechanic (P4-11). Bound from
/// <c>appsettings.json</c> section <c>"Gamification:Freeze"</c>.
///
/// Mirrors <see cref="StreakOptions"/> in shape. Defaults are valid out-of-the-box;
/// override in environment-specific appsettings as needed.
///
/// Note: <c>MaxInventory</c> is exposed here for observability and doc purposes.
/// The hard schema-level cap (DB CHECK constraint &lt;= 2) is enforced via the
/// <c>StudentXpProfile.MaxFreezes = 2</c> constant, which is the entity invariant.
/// Making the entity-level cap configurable here could exceed the DB CHECK without a
/// migration, so <c>MaxInventory</c> is informational — the entity constant governs.
/// </summary>
public sealed class FreezeOptions
{
    /// <summary>Config section path — matches the appsettings.json key.</summary>
    public const string SectionName = "Gamification:Freeze";

    /// <summary>
    /// Maximum freezes a student can hold at once. Default 2.
    /// Informational: the entity-level cap is <c>StudentXpProfile.MaxFreezes</c>;
    /// the DB CHECK constraint enforces &lt;= 2 at the schema level.
    /// </summary>
    public int MaxInventory { get; set; } = 2;

    /// <summary>
    /// Streak length multiple that triggers an auto-grant. Default 7.
    /// A freeze is granted whenever <c>CurrentStreak % EarnEveryNStreakDays == 0</c>.
    /// </summary>
    public int EarnEveryNStreakDays { get; set; } = 7;
}

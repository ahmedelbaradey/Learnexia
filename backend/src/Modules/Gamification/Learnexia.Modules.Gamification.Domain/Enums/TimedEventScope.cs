namespace Learnexia.Modules.Gamification.Domain.Enums;

/// <summary>
/// Defines which XP reason(s) a <c>TimedEvent</c> multiplier applies to.
///
/// Only <see cref="AllXp"/> is honored by <c>IXpBoostCalculator</c> in P4-11 (Phase 3 MVP).
/// <see cref="MissionXp"/> and <see cref="LeagueXp"/> are forward-compat declarations included
/// so the DB schema is stable for the P5 analytics pipeline and P7 admin editor without a
/// future migration; the calculator no-ops on those values until explicitly wired.
/// </summary>
public enum TimedEventScope
{
    /// <summary>Multiplier applies to every XP award regardless of reason.</summary>
    AllXp = 1,

    /// <summary>Multiplier applies only to XP from mission completions (forward-compat — P5+).</summary>
    MissionXp = 2,

    /// <summary>Multiplier applies only to XP from league contributions (forward-compat — P5+).</summary>
    LeagueXp = 3
}

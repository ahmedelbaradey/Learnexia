namespace Learnexia.Modules.Gamification.Application.Caching;

/// <summary>
/// Centralized key builders. Convention: gamification:student:{id}:{seam}[:{discriminator}].
/// All student-scoped helpers take <c>int studentId</c> — the actual PK type on
/// <c>StudentXpProfile</c> and used by every query seam.
/// </summary>
public static class GamificationCacheKeys
{
    private const string Prefix = "gamification";

    public static string Xp(int studentId)        => $"{Prefix}:student:{studentId}:xp";
    public static string Streak(int studentId)    => $"{Prefix}:student:{studentId}:streak";
    public static string Hearts(int studentId)    => $"{Prefix}:student:{studentId}:hearts";
    public static string Badges(int studentId, int take = 3)
        => $"{Prefix}:student:{studentId}:badges:{take}";
    public static string Missions(int studentId, string periodKey)
        => $"{Prefix}:student:{studentId}:missions:{periodKey}";
    public static string LeagueSnapshot(int studentId, string periodKey)
        => $"{Prefix}:student:{studentId}:league:{periodKey}";
    public static string LeaderboardSortedSet(int leagueId)
        => $"{Prefix}:league:{leagueId}:standings";
}

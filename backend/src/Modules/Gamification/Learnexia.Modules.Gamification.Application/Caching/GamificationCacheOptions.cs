namespace Learnexia.Modules.Gamification.Application.Caching;

public sealed class GamificationCacheOptions
{
    public const string SectionName = "Gamification:Cache";

    public bool Enabled { get; set; } = true;
    public int XpTtlSeconds { get; set; } = 60;
    public int StreakTtlSeconds { get; set; } = 60;
    public int HeartsTtlSeconds { get; set; } = 30;
    public int BadgesTtlSeconds { get; set; } = 300;
    public int MissionsTtlSeconds { get; set; } = 60;
    public int LeagueSnapshotTtlSeconds { get; set; } = 30;
    public int LeaderboardSortedSetTtlSeconds { get; set; } = 691200; // 8 days

    /// <summary>
    /// TTL for the global active-timed-events cache key (<c>gamification:timed_events:active</c>).
    /// Short TTL (default 30s) so events transition cleanly within one sweep-job cycle (2 min).
    /// P4-11-B1-B.
    /// </summary>
    public int TimedEventsTtlSeconds { get; set; } = 30;
}

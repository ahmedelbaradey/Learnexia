using Learnexia.Shared.Contracts.Gamification;

namespace Learnexia.Modules.Identity.Application.Features.Users.Dtos.Admin;

/// <summary>
/// Activity summary DTO for the admin activity endpoint (P7-06 BE-4).
/// Composed from Gamification seam queries; each field is best-effort
/// (null/empty when the seam throws or has no data for this student).
/// Never returns 500 — a missing seam degrades gracefully to "no data".
///
/// <c>LastSignInAtUtc</c> is always null — the User entity has no LastSignInAt column
/// (P7-06 Q-A6 baked-in decision; label as "not tracked" in the UI).
/// </summary>
public sealed record AdminActivitySummaryDto
{
    public int UserId { get; init; }

    /// <summary>
    /// Always null — last sign-in timestamp is not tracked at the Identity layer.
    /// Clients should label this field as "Sign-in activity: not tracked".
    /// </summary>
    public DateTime? LastSignInAtUtc => null;

    // ── Gamification seam data (each is best-effort / null on seam failure) ──

    public XpSummary? Xp { get; init; }
    public StreakSummary? Streak { get; init; }
    public BadgesSummary? Badges { get; init; }
    public MissionsSummary? Missions { get; init; }
    public LeagueSummary? League { get; init; }
}

/// <summary>XP summary from <c>IStudentXpQuery</c>.</summary>
public sealed record XpSummary(int TotalXp, int CurrentLevel);

/// <summary>Streak summary from <c>IStudentStreakQuery</c>.</summary>
public sealed record StreakSummary(int CurrentStreak, int LongestStreak, DateOnly? LastActivityDateUtc);

/// <summary>Badges summary from <c>IStudentBadgesQuery</c>.</summary>
public sealed record BadgesSummary(int TotalCount);

/// <summary>Missions summary (counts only) from <c>IStudentMissionsQuery</c>.</summary>
public sealed record MissionsSummary(int DailyMissionCount, int CompletedDailyMissions);

/// <summary>League summary from <c>IStudentLeagueQuery</c>.</summary>
public sealed record LeagueSummary(string Tier, int WeeklyXp, int CurrentRank, int GroupSize);

namespace Learnexia.Modules.Gamification.Application.Features.Leagues.Queries.GetMyLeague;

/// <summary>
/// Single ranked entry in the league standings list (P4-07, AC3).
///
/// <para><b>Anonymization (D2):</b> display name is always <c>"Student #N"</c> where
/// <c>N = LeagueMembership.JoinOrder</c> — a stable 1-based cohort ordinal assigned at join time.
/// No PII (FullName or Username) is exposed. This is by design for child accounts.</para>
///
/// Mirrors <see cref="Missions.Queries.GetMyMissions.MissionStateDto"/> shape
/// (flat sealed record with all relevant fields).
/// </summary>
/// <param name="Rank">1-based rank within the cohort, ordered WeeklyXp DESC / JoinedAtUtc ASC (D4).</param>
/// <param name="DisplayName">Anonymized: "Student #N" where N is the stable JoinOrder.</param>
/// <param name="WeeklyXp">XP earned in the current week's competition period.</param>
/// <param name="IsMe">True when this entry corresponds to the caller (JWT-resolved student).</param>
public sealed record LeagueStandingDto(
    int Rank,
    string DisplayName,
    int WeeklyXp,
    bool IsMe);

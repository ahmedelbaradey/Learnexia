using Learnexia.Shared.Contracts.Gamification;

namespace Learnexia.Modules.Learning.Application.Features.Dashboard.Dtos;

/// <summary>
/// Home dashboard aggregation DTO for a student.
/// Phase-4 shape: Xp/Level wired via IStudentXpQuery (P4-02); Streak via IStudentStreakQuery (P4-03);
/// Hearts/InPracticeMode via IStudentHeartsQuery (P4-04); BadgesCount/RecentBadges via IStudentBadgesQuery (P4-05);
/// DailyMissions/WeeklyMission via IStudentMissionsQuery (P4-06);
/// LeaguePreview via IStudentLeagueQuery (P4-07) — null only for brand-new students with no league profile.
/// Continue is null only when no Available lesson exists anywhere in the student's grade-1 subjects.
///
/// Positional record — new fields are appended with defaults (additive change, non-breaking).
/// P4-06: The old Phase-2 stub field <c>DailyMission</c> (typed <c>DailyMissionDto?</c>, always null)
/// has been replaced by <c>DailyMissions</c> (list — lazy-instantiated current-period missions)
/// and <c>WeeklyMission</c> (single summary). The old <c>DailyMissionDto</c> type has been deleted.
/// Api-client regen needed after this change.
/// </summary>
public record DashboardDto(
    int Xp,                              // real XP from gamification module (P4-02)
    int Streak,                          // real streak from gamification module (P4-03)
    LeaguePreviewDto? LeaguePreview,     // P4-07 — null only for brand-new students with no league profile
    ContinueTargetDto? Continue,         // null only when no Available lesson exists
    int Level = 1,                       // P4-02 — computed from XP via LevelCurve; default 1 for new students
    int Hearts = 5,                      // P4-04 — real hearts from gamification module; default 5 = Cap for new students
    bool InPracticeMode = false,         // P4-04 — derived from Hearts == 0 in gamification module
    int BadgesCount = 0,                 // P4-05 — total badges earned from gamification module; default 0 for new students
    IReadOnlyList<BadgeSummary>? RecentBadges = null,  // P4-05 — top 3 recent badges (null = none earned yet)
    IReadOnlyList<MissionSummary>? DailyMissions = null,  // P4-06 — current-period daily missions (null = brand-new student / not yet loaded)
    MissionSummary? WeeklyMission = null  // P4-06 — top weekly mission summary (null = brand-new student / not yet loaded)
);

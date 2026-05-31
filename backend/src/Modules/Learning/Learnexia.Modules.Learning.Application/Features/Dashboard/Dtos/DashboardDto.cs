using Learnexia.Shared.Contracts.Gamification;

namespace Learnexia.Modules.Learning.Application.Features.Dashboard.Dtos;

/// <summary>
/// Home dashboard aggregation DTO for a student.
/// Phase-4 shape: Xp/Level wired via IStudentXpQuery (P4-02); Streak via IStudentStreakQuery (P4-03);
/// Hearts/InPracticeMode via IStudentHeartsQuery (P4-04); BadgesCount/RecentBadges via IStudentBadgesQuery (P4-05).
/// DailyMission/LeaguePreview null (P4-06/P4-07).
/// Continue is null only when no Available lesson exists anywhere in the student's grade-1 subjects.
///
/// Positional record — new fields are appended with defaults (additive change, non-breaking).
/// Api-client regen needed after this change.
/// </summary>
public record DashboardDto(
    int Xp,                              // real XP from gamification module (P4-02)
    int Streak,                          // real streak from gamification module (P4-03)
    DailyMissionDto? DailyMission,       // null — TODO P4-06
    LeaguePreviewDto? LeaguePreview,     // null — TODO P4-07
    ContinueTargetDto? Continue,         // null only when no Available lesson exists
    int Level = 1,                       // P4-02 — computed from XP via LevelCurve; default 1 for new students
    int Hearts = 5,                      // P4-04 — real hearts from gamification module; default 5 = Cap for new students
    bool InPracticeMode = false,         // P4-04 — derived from Hearts == 0 in gamification module
    int BadgesCount = 0,                 // P4-05 — total badges earned from gamification module; default 0 for new students
    IReadOnlyList<BadgeSummary>? RecentBadges = null  // P4-05 — top 3 recent badges (null = none earned yet)
);

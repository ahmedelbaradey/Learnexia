namespace Learnexia.Modules.Learning.Application.Features.Dashboard.Dtos;

/// <summary>
/// Home dashboard aggregation DTO for a student.
/// Phase-4 shape: Xp/Level wired via IStudentXpQuery (P4-02); Streak still 0 (P4-03);
/// DailyMission/LeaguePreview still null (P4-06/P4-07).
/// Continue is null only when no Available lesson exists anywhere in the student's grade-1 subjects.
/// </summary>
public record DashboardDto(
    int Xp,                              // real XP from gamification module (P4-02)
    int Streak,                          // 0 in Phase 2 — TODO P4-03
    DailyMissionDto? DailyMission,       // null in Phase 2 — TODO P4-06
    LeaguePreviewDto? LeaguePreview,     // null in Phase 2 — TODO P4-07
    ContinueTargetDto? Continue,         // null only when no Available lesson exists
    int Level = 1                        // P4-02 — computed from XP via LevelCurve; default 1 for new students
);

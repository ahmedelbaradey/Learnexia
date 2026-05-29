namespace Learnexia.Modules.Learning.Application.Features.Dashboard.Dtos;

/// <summary>
/// Home dashboard aggregation DTO for a student.
/// Phase-2 shape: Xp/Streak always 0; DailyMission/LeaguePreview always null.
/// Continue is null only when no Available lesson exists anywhere in the student's grade-1 subjects.
/// Phase-4 wiring: P4-02 (Xp), P4-03 (Streak), P4-06 (DailyMission), P4-07 (LeaguePreview).
/// </summary>
public record DashboardDto(
    int Xp,                              // 0 in Phase 2 — TODO P4-02
    int Streak,                          // 0 in Phase 2 — TODO P4-03
    DailyMissionDto? DailyMission,       // null in Phase 2 — TODO P4-06
    LeaguePreviewDto? LeaguePreview,     // null in Phase 2 — TODO P4-07
    ContinueTargetDto? Continue          // null only when no Available lesson exists
);

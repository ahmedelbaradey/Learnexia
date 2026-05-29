namespace Learnexia.Modules.Learning.Application.Features.Dashboard.Dtos;

/// <summary>
/// Phase-2 stub. Always null in Phase 2 — populated by P4-06 (daily mission engine).
/// Fields are Phase-4 hints only; exact schema may change.
/// </summary>
public record DailyMissionDto(
    string? Type,        // e.g. "AnswerQuestions", "CompleteLesson" — TODO P4-06
    int? Target,         // required answers/lessons count
    int? Progress        // student's current progress toward target
);

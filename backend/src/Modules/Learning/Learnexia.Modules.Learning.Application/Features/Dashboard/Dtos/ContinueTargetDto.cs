using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.Dashboard.Dtos;

/// <summary>
/// Identifies the next Available lesson the student should continue.
/// NodeState is always Available for a Continue target (never Locked or Completed).
/// SkillId/SkillName are null when the lesson has no linked skill (Lesson.SkillId IS NULL).
/// </summary>
public record ContinueTargetDto(
    int SubjectId,
    string SubjectName,
    int UnitId,
    string UnitName,
    int LessonId,
    string LessonName,
    int? SkillId,
    string? SkillName,
    NodeState NodeState,       // always NodeState.Available
    bool IsBoss                // true when the Continue target is a boss lesson (P2-03)
);

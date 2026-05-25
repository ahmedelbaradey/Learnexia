using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;

/// <summary>
/// Student-facing lesson summary nested inside <see cref="UnitWithLessonsDto"/>.
/// </summary>
public record LessonInUnitDto
{
    public int LessonId { get; init; }
    public string Name { get; init; } = null!;
    public DifficultyLevel Difficulty { get; init; }
    public int SequenceOrder { get; init; }
    public bool IsLocked { get; init; }
    public int? SkillId { get; init; }
}

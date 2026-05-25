namespace Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;

/// <summary>
/// Student-facing unit with its ordered lessons. Returned by GetSubjectLessonsQuery.
/// </summary>
public record UnitWithLessonsDto
{
    public int UnitId { get; init; }
    public string Name { get; init; } = null!;
    public int SequenceOrder { get; init; }
    public List<LessonInUnitDto> Lessons { get; init; } = new();
}

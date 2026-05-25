namespace Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;

/// <summary>
/// Student-facing subject summary returned by GetSubjectsForGradeQuery.
/// Intentionally separate from the admin <see cref="SubjectDto"/> to keep the two contracts independent.
/// </summary>
public record StudentSubjectDto
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public int GradeNumber { get; init; }
}

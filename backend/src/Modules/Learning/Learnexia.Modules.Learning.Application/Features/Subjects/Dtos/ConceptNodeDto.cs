namespace Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;

/// <summary>
/// Student-facing concept node returned by GetSubjectSkillTreeQuery.
/// Contains an ordered list of <see cref="SkillNodeDto"/> children.
/// Ordered by Concept.Id ascending (surrogate for the absent SequenceOrder column — deferred to P2-11).
/// </summary>
public record ConceptNodeDto
{
    public int ConceptId { get; init; }
    public string Name { get; init; } = null!;
    public List<SkillNodeDto> Skills { get; init; } = new();
}

using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Dtos;

/// <summary>
/// Student-facing read model for a knowledge-graph node, returned by the
/// <c>GetPrerequisites</c> and <c>GetUnlockedBy</c> student-reachable ([Authorize]) endpoints.
///
/// Security (P7-03): intentionally omits <see cref="KnowledgeNodeDto.IsSkillActive"/> so that
/// inactive-skill existence is not disclosed to students. Inactive-skill nodes are also filtered
/// out by the handlers before mapping — this DTO is the outer contract that ensures the flag
/// cannot be added back accidentally.
/// </summary>
public record StudentKnowledgeNodeDto
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public KnowledgeNodeType NodeType { get; init; }
    public int SubjectId { get; init; }
    public int GradeId { get; init; }
    public int Difficulty { get; init; }
    public int? SkillId { get; init; }
}

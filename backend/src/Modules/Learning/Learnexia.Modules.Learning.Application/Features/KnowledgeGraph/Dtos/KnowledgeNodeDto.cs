using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Dtos;

/// <summary>
/// Read model returned by the <c>GetPrerequisites</c> and <c>GetUnlockedBy</c> queries.
/// Fields mirror <c>KnowledgeNode</c> entity properties (P2-11 BE-5).
/// </summary>
public record KnowledgeNodeDto
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public KnowledgeNodeType NodeType { get; init; }
    public int SubjectId { get; init; }
    public int GradeId { get; init; }
    public int Difficulty { get; init; }
    public int? SkillId { get; init; }
}

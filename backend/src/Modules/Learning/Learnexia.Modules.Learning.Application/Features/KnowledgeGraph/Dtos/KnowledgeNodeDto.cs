using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Dtos;

/// <summary>
/// Read model returned by the <c>GetPrerequisites</c>, <c>GetUnlockedBy</c>, and <c>GetGraph</c> queries.
/// Fields mirror <c>KnowledgeNode</c> entity properties (P2-11 BE-5).
///
/// P7-03: Added <see cref="IsSkillActive"/> — true when the wrapping Skill is active (or when
/// this node does not wrap a Skill). Admin graph uses this to flag inactive-skill nodes.
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

    /// <summary>
    /// P7-03: Whether the Skill wrapped by this node is active.
    /// <c>true</c> for non-skill nodes (Concept/Review) and for nodes wrapping active skills.
    /// <c>false</c> only for nodes wrapping a Skill whose <c>IsActive == false</c>.
    /// Admin graph shows all non-deleted nodes; this flag lets the UI visually mark inactive-skill nodes.
    /// </summary>
    public bool IsSkillActive { get; init; } = true;
}

using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Dtos;

/// <summary>
/// Read model for a single edge in the skill dependency graph (P7-03).
/// Returned by <c>GetGraphQuery</c> and included in <c>SkillGraphDto</c>.
/// </summary>
public record KnowledgeEdgeDto
{
    public int Id { get; init; }
    public int SourceNodeId { get; init; }
    public int TargetNodeId { get; init; }
    public EdgeRelationshipType RelationshipType { get; init; }
    public decimal Strength { get; init; }
}

namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Dtos;

/// <summary>
/// Composite read model returned by <c>GetGraphQuery</c> (P7-03).
/// Contains all live (non-deleted) nodes and edges for a given subject.
/// Admin view: includes nodes that wrap inactive skills (flagged via <see cref="KnowledgeNodeDto.IsActive"/>).
/// Student-facing reads filter out inactive skills; the graph query is admin-only.
/// </summary>
public record SkillGraphDto
{
    public int SubjectId { get; init; }
    public IReadOnlyList<KnowledgeNodeDto> Nodes { get; init; } = [];
    public IReadOnlyList<KnowledgeEdgeDto> Edges { get; init; } = [];
}

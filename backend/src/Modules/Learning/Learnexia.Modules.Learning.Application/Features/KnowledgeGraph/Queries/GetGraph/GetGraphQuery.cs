using Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Queries.GetGraph;

/// <summary>
/// Returns the full admin skill dependency graph for a subject.
/// Nodes and edges are returned for all non-deleted rows in the subject, including those
/// wrapping inactive skills (admin view). Edges are all non-deleted edges between subject nodes.
///
/// Queries are NOT auto-validated (CONVENTIONS §4). Input validation is done in the handler.
/// </summary>
public record GetGraphQuery : IQuery<BaseResponse<SkillGraphDto>>
{
    /// <summary>
    /// The subject whose graph to return. Required (must be &gt; 0).
    /// </summary>
    public int SubjectId { get; init; }
}

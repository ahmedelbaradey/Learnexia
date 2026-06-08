using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Commands.AddEdge;

/// <summary>
/// Creates a new directed edge between two existing <see cref="Domain.Entities.KnowledgeNode"/>s.
/// The command takes node IDs (not skill IDs) — callers resolve Skill → Node before calling.
///
/// Validation applied by <see cref="AddKnowledgeEdgeCommandValidator"/>:
///   - SourceNodeId and TargetNodeId must be &gt; 0.
///   - Strength (if supplied) must be in [0.0, 1.0].
///
/// Handler guards (returning Successed=false, NOT throwing):
///   - Both nodes must exist (non-deleted).
///   - Both nodes must share the same language tree (cross-language edge rejected).
///   - For Prerequisite edges: adding this edge must not create a cycle (acyclic guard via SkillGraphValidator).
///   - The (SourceNodeId, TargetNodeId, RelationshipType) triple must be unique.
/// </summary>
public record AddKnowledgeEdgeCommand : ICommand<BaseResponse<string>>
{
    public int SourceNodeId { get; init; }
    public int TargetNodeId { get; init; }
    public EdgeRelationshipType RelationshipType { get; init; }

    /// <summary>
    /// Edge weight in [0.0, 1.0]. Defaults to 1.0 when absent.
    /// </summary>
    public decimal? Strength { get; init; }
}

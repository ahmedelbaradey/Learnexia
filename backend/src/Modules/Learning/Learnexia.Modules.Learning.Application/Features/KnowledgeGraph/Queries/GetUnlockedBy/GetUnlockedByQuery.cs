using Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Queries.GetUnlockedBy;

/// <summary>
/// Returns the set of <see cref="StudentKnowledgeNodeDto"/> that mastering the given node unlocks
/// (targets of Prerequisite edges whose SourceNodeId == NodeId).
/// Student-facing: inactive-skill nodes are filtered out; IsSkillActive is not exposed.
/// Queries are NOT auto-validated (CONVENTIONS §4).
/// </summary>
public record GetUnlockedByQuery(int NodeId) : IQuery<BaseResponse<List<StudentKnowledgeNodeDto>>>;

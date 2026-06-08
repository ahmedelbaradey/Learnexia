using Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Queries.GetPrerequisites;

/// <summary>
/// Returns the set of <see cref="StudentKnowledgeNodeDto"/> that are prerequisites of the given node
/// (sources of Prerequisite edges whose TargetNodeId == NodeId).
/// Student-facing: inactive-skill nodes are filtered out; IsSkillActive is not exposed.
/// Queries are NOT auto-validated (CONVENTIONS §4).
/// </summary>
public record GetPrerequisitesQuery(int NodeId) : IQuery<BaseResponse<List<StudentKnowledgeNodeDto>>>;

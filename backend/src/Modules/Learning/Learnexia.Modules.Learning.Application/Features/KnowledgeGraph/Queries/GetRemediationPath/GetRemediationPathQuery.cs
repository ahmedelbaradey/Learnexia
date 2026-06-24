using Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Queries.GetRemediationPath;

/// <summary>
/// Returns the transitive prerequisite chain ("remediation path") for a given node (BL-03-BE-5).
///
/// <para>Given a node id for which a student is struggling, walks upstream BFS through
/// Prerequisite edges (Source→Target means Source must be mastered before Target — so we walk
/// the source side of edges for which our current node is the target). Returns up to
/// <c>KnowledgeGraph:RemediationMaxDepth</c> levels deep, cycle-guarded.</para>
///
/// <para>Student-facing: [Authorize] (not AdminOnly). Inactive-skill nodes are filtered out.</para>
/// <para>Queries are NOT auto-validated (CONVENTIONS §4).</para>
/// </summary>
public record GetRemediationPathQuery(int NodeId) : IQuery<BaseResponse<List<RemediationPathDto>>>;

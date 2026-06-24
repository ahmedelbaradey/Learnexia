using Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Queries.GetRelatedConcepts;

/// <summary>
/// Returns the set of <see cref="StudentKnowledgeNodeDto"/> connected to the given node via
/// <see cref="Learnexia.Modules.Learning.Domain.Enums.EdgeRelationshipType.Related"/> edges
/// (either direction). Student-facing: inactive-skill nodes are filtered out.
/// (BL-03-BE-4)
///
/// <para>Confirmed query-API gap — only Prerequisites + Dependents exist before BL-03.</para>
/// <para>Queries are NOT auto-validated (CONVENTIONS §4).</para>
/// </summary>
public record GetRelatedConceptsQuery(int NodeId) : IQuery<BaseResponse<List<StudentKnowledgeNodeDto>>>;

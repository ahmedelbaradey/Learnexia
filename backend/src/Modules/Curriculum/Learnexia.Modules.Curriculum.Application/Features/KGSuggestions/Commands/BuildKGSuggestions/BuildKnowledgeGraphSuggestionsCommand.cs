using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Commands.BuildKGSuggestions;

/// <summary>
/// Admin/system command to enqueue an <c>infer_edges</c> <see cref="Learnexia.Modules.Curriculum.Domain.Entities.PipelineJob"/>
/// for a specific subject+grade (BL-03-BE-3).
///
/// <para>Flow:
/// <list type="number">
///   <item>Load KnowledgeNode rows for the given SubjectCode + GradeId via <c>IKnowledgeNodeReader</c>
///         (cross-module read seam — no project ref to Learning).</item>
///   <item>Guard: there must be at least one node with a SkillKey (otherwise inference has nothing to work with).</item>
///   <item>Serialize the node list (SkillKey-keyed) as <c>PayloadJson</c>.</item>
///   <item>Write a new <c>PipelineJob{JobType='infer_edges', Status='Pending'}</c>.</item>
/// </list>
/// </para>
///
/// <para><strong>Decision E invariant:</strong> this command NEVER writes <c>KGSuggestion</c> or
/// <c>KnowledgeEdge</c>. It only enqueues the job for the Python worker, which returns candidates
/// in <c>ResultJson</c>. <see cref="Jobs.EdgeInferenceAdvanceService"/> then claims Done jobs and
/// writes <c>KGSuggestion{Pending}</c>. The ONLY path to <c>KnowledgeEdge</c> is admin approval.</para>
///
/// <para>Admin-only command (validated; see <see cref="BuildKnowledgeGraphSuggestionsCommandValidator"/>).</para>
/// </summary>
public record BuildKnowledgeGraphSuggestionsCommand(
    /// <summary>SubjectCode int: 0=Math, 1=Science, 2=Arabic, 3=English.</summary>
    int SubjectCode,
    /// <summary>Learning Grade.Id (plain int, no cross-module FK).</summary>
    int GradeId)
    : ICommand<BaseResponse<string>>;

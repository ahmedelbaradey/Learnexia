using Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Commands.ApproveKGSuggestion;

/// <summary>
/// Admin command to approve a pending <c>KGSuggestion</c>, publishing it as a
/// <c>KnowledgeEdge</c> in the learning module (BL-03-BE-8).
///
/// <para><strong>Decision E chokepoint:</strong> this command is the SINGLE code path that
/// can produce a new <c>KnowledgeEdge</c> outside the <c>learning</c> module.
/// It calls <see cref="Learnexia.Shared.Contracts.Learning.IKnowledgeEdgeWriter.PublishApprovedEdgeAsync"/>
/// which enforces all guards (cross-language, duplicate, acyclic) inside <c>learning</c>
/// Infrastructure.</para>
///
/// <para>Prerequisites:
/// <list type="bullet">
///   <item>Suggestion must exist (404 otherwise).</item>
///   <item>Suggestion must be in <see cref="Learnexia.Modules.Curriculum.Domain.Enums.KGSuggestionStatus.Pending"/>
///         (409 if already resolved).</item>
/// </list>
/// </para>
///
/// <para>On success: suggestion transitions to Approved; ReviewedByUserId + ReviewedAt stamped atomically
/// with the edge publish in a single explicit transaction.</para>
///
/// <para>Cycle or cross-language → 422 domain error; neither edge nor status is committed.</para>
/// </summary>
public record ApproveKGSuggestionCommand(int SuggestionId, string? ReviewNotes)
    : ICommand<BaseResponse<KGSuggestionDto>>;

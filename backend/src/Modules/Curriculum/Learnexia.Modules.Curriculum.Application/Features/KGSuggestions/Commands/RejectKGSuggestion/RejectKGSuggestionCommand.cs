using Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Commands.RejectKGSuggestion;

/// <summary>
/// Admin command to reject a pending <c>KGSuggestion</c> (BL-03-BE-9).
///
/// <para>Rejected suggestions are permanently withheld — no edge is published.
/// To regenerate, trigger a new <c>BuildKnowledgeGraphSuggestionsCommand</c>.</para>
///
/// <para>Prerequisites:
/// <list type="bullet">
///   <item>Suggestion must exist (404 otherwise).</item>
///   <item>Suggestion must be in <see cref="Learnexia.Modules.Curriculum.Domain.Enums.KGSuggestionStatus.Pending"/>
///         (409 if already resolved).</item>
/// </list>
/// </para>
/// </summary>
public record RejectKGSuggestionCommand(int SuggestionId, string? ReviewNotes)
    : ICommand<BaseResponse<KGSuggestionDto>>;

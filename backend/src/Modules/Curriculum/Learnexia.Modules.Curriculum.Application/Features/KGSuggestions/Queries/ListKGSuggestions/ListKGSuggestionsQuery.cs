using Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Dtos;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Queries.ListKGSuggestions;

/// <summary>
/// Returns a paged list of <c>KGSuggestion</c> rows for the admin review queue (BL-03-BE-7).
///
/// <para>Optionally filtered by <see cref="Status"/>, <see cref="SubjectCode"/>, and
/// <see cref="GradeId"/>. Default: all Pending suggestions, paged.</para>
///
/// <para>This is an <see cref="IQuery{TResponse}"/> — ValidationBehavior does NOT run
/// (queries are not auto-validated). The handler performs its own guards.</para>
/// </summary>
public class ListKGSuggestionsQuery : IQuery<PaginatedResult<KGSuggestionDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    /// <summary>Optional filter — only suggestions in this status (default: Pending only).</summary>
    public KGSuggestionStatus? Status { get; init; }

    /// <summary>Optional filter — only suggestions whose SourceNodeId belongs to this SubjectCode.
    /// Note: KGSuggestion stores only NodeIds (no SubjectCode). This filter is left for future use;
    /// currently unused if not joined to Learning nodes. Pass null to omit.</summary>
    public int? SubjectCode { get; init; }

    /// <summary>Optional filter — only suggestions whose nodes belong to this GradeId.
    /// Same note as SubjectCode — pure curriculum data has no GradeId on KGSuggestion.</summary>
    public int? GradeId { get; init; }
}

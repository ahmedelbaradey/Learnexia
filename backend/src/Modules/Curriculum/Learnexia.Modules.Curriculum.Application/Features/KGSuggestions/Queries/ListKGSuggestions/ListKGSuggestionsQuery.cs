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
/// <para>Subject+grade filtering is only applied when BOTH <see cref="SubjectCode"/> and
/// <see cref="GradeId"/> are provided. The handler resolves matching KnowledgeNode ids via
/// <c>IKnowledgeNodeReader</c> and returns only suggestions whose <c>SourceNodeId</c> or
/// <c>TargetNodeId</c> belongs to those nodes.</para>
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

    /// <summary>Optional filter — only suggestions whose source or target node belongs to this SubjectCode
    /// (resolved via <c>IKnowledgeNodeReader.GetNodesForSubjectAsync</c>). Requires <see cref="GradeId"/>
    /// to be set as well; ignored when <see cref="GradeId"/> is absent. SubjectCode int: 0=Math,
    /// 1=Science, 2=Arabic, 3=English (matches Learning's SubjectCode enum).</summary>
    public int? SubjectCode { get; init; }

    /// <summary>Optional filter — paired with <see cref="SubjectCode"/> to narrow suggestions to a
    /// specific grade (Learning module Grade.Id). Both params must be set for the filter to apply.</summary>
    public int? GradeId { get; init; }
}

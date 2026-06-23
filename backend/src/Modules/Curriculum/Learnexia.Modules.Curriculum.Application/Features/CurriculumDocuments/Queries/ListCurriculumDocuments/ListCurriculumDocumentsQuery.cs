using Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Dtos;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Queries.ListCurriculumDocuments;

/// <summary>
/// Returns a paged list of curriculum documents, optionally filtered by
/// <see cref="Status"/>, <see cref="GradeId"/>, and/or <see cref="SubjectId"/>.
///
/// <para>This is an <see cref="IQuery{TResponse}"/> — ValidationBehavior does NOT run
/// (queries are not auto-validated). The handler performs its own guards.</para>
/// </summary>
public class ListCurriculumDocumentsQuery : IQuery<PaginatedResult<CurriculumDocumentListDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    /// <summary>Optional filter by document pipeline status.</summary>
    public DocumentStatus? Status { get; init; }

    /// <summary>Optional filter by grade (cross-module plain int ref).</summary>
    public int? GradeId { get; init; }

    /// <summary>Optional filter by subject (cross-module plain int ref).</summary>
    public int? SubjectId { get; init; }
}

using Learnexia.Modules.Curriculum.Application.Features.IngestionReview.Dtos;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Curriculum.Application.Features.IngestionReview.Queries.GetIngestionReviewItems;

/// <summary>
/// Returns a paged list of <c>IngestionReviewItem</c> rows for the admin review queue
/// (BL-05-BE-7).
///
/// <para>Optionally filtered by <see cref="DocumentId"/>, <see cref="Status"/>, and
/// <see cref="VersionId"/>. Default: all Pending items, paged.</para>
///
/// <para>This is an <see cref="IQuery{TResponse}"/> — ValidationBehavior does NOT run
/// (queries are not auto-validated). The handler performs its own guards.</para>
/// </summary>
public class GetIngestionReviewItemsQuery : IQuery<PaginatedResult<IngestionReviewItemDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    /// <summary>Optional filter — only items for a specific CurriculumDocument.</summary>
    public int? DocumentId { get; init; }

    /// <summary>Optional filter — only items in a specific ReviewStatus (default: Pending only).</summary>
    public ReviewStatus? Status { get; init; }

    /// <summary>Optional filter — only items linked to a specific CurriculumVersion.</summary>
    public int? VersionId { get; init; }
}

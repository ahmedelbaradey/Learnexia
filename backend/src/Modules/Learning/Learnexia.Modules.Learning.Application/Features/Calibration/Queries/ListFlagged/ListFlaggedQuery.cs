using Learnexia.Modules.Learning.Application.Features.Calibration.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Calibration.Queries.ListFlagged;

/// <summary>
/// Query: list paged flagged questions (<c>QualityState == FlaggedForReview</c>).
///
/// Queries are NOT auto-validated by ValidationBehavior. Inputs validated in-handler.
/// </summary>
public record ListFlaggedQuery : IQuery<BaseResponse<PaginatedResult<FlaggedQuestionDto>>>
{
    public int Page     { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

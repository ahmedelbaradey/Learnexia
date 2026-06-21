using Learnexia.Modules.Learning.Application.Features.Calibration.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Calibration.Queries.ListProposals;

/// <summary>
/// Query: list paged recalibration proposals, optionally filtered by status.
///
/// Queries are NOT auto-validated by ValidationBehavior (CLAUDE rule §4/§13).
/// Inputs are validated in-handler.
///
/// <paramref name="Status"/>: optional filter. When null, returns all statuses.
///   1=Pending, 2=Promoted, 3=Rejected. Out-of-range values are ignored (returns all).
/// </summary>
public record ListProposalsQuery : IQuery<BaseResponse<PaginatedResult<CalibrationProposalDto>>>
{
    public int?  Status   { get; init; }
    public int   Page     { get; init; } = 1;
    public int   PageSize { get; init; } = 20;
}

using Learnexia.Modules.Billing.Application.Features.BillingHistory.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.BillingHistory.Queries.GetBillingHistory;

/// <summary>
/// Query: returns a chronological (newest-first), paginated list of the authenticated
/// parent's payment and refund entries.
///
/// <para>IDOR scoping: <c>ParentUserId</c> is always sourced from the JWT by the handler —
/// never from the request body. <see cref="Abstractions.IBillingHistoryQueryService.GetHistoryAsync"/>
/// enforces the ownership filter server-side.</para>
///
/// <para>Queries are NOT auto-validated by <c>ValidationBehavior</c> — paging defaults are
/// applied defensively in the handler.</para>
/// </summary>
public sealed record GetBillingHistoryQuery : IQuery<BaseResponse<PaginatedResult<BillingHistoryItemDto>>>
{
    /// <summary>Owning parent's user id (resolved from JWT in the handler).</summary>
    public int ParentUserId { get; init; }

    /// <summary>1-based page number. Defaults to 1.</summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>Items per page. Defaults to 20; capped at 100 internally.</summary>
    public int PageSize { get; init; } = 20;
}

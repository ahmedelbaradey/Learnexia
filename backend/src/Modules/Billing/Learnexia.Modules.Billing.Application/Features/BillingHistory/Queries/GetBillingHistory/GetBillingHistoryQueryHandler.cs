using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.BillingHistory.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.BillingHistory.Queries.GetBillingHistory;

/// <summary>
/// Handles <see cref="GetBillingHistoryQuery"/>.
///
/// <para><strong>Option C — handler is EF-free.</strong> All query, projection, pagination,
/// and IDOR-scoping logic lives in <see cref="IBillingHistoryQueryService"/>. This handler only:
/// <list type="number">
///   <item>Resolves <c>parentId</c> from the JWT via <see cref="ICurrentUserService"/>.</item>
///   <item>Applies defensive paging defaults.</item>
///   <item>Calls <see cref="IBillingHistoryQueryService.GetHistoryAsync"/> and wraps in
///         <see cref="BaseResponse{T}"/>.</item>
/// </list>
/// </para>
///
/// <para>No <c>IBillingDbContext</c>, <c>DbSet</c>, EF Core, or AutoMapper references appear
/// in this file.</para>
/// </summary>
public sealed class GetBillingHistoryQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetBillingHistoryQuery, BaseResponse<PaginatedResult<BillingHistoryItemDto>>>
{
    private readonly IBillingHistoryQueryService _historyService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetBillingHistoryQueryHandler(
        IBillingHistoryQueryService historyService,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _historyService = historyService;
        _currentUser    = currentUser;
        _logger         = logger;
        _localizer      = localizer;
    }

    public async Task<BaseResponse<PaginatedResult<BillingHistoryItemDto>>> Handle(
        GetBillingHistoryQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // ── parentId is ALWAYS from the JWT — never from the request body ───────
            var parentId = _currentUser.UserId;
            if (!parentId.HasValue || parentId.Value <= 0)
            {
                return Unauthorized<PaginatedResult<BillingHistoryItemDto>>(
                    _localizer[SharedResourcesKey.Unauthorized]);
            }

            // ── Defensive paging ────────────────────────────────────────────────────
            var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
            var pageSize   = request.PageSize < 1 ? 20 : Math.Min(request.PageSize, 100);

            // ── Delegate to service (EF lives there) ────────────────────────────────
            var result = await _historyService.GetHistoryAsync(
                parentId.Value,
                pageNumber,
                pageSize,
                cancellationToken);

            _logger.LogInfo(
                $"GetBillingHistory: parentId={parentId.Value}, page={pageNumber}, count={result.Data?.Count ?? 0}.");

            return new BaseResponse<PaginatedResult<BillingHistoryItemDto>>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.BillingHistoryRetrievedSuccessfully],
                Data       = result,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in GetBillingHistoryQuery for parentId={_currentUser.UserId}.");
            return ServerError<PaginatedResult<BillingHistoryItemDto>>(
                _localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}

using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.BillingHistory.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.BillingHistory.Queries.GetReceipt;

/// <summary>
/// Handles <see cref="GetReceiptQuery"/>.
///
/// <para><strong>Option C — handler is EF-free.</strong> All query, IDOR-check, and projection
/// logic lives in <see cref="IBillingHistoryQueryService"/>. This handler only:
/// <list type="number">
///   <item>Resolves <c>parentId</c> from the JWT via <see cref="ICurrentUserService"/>.</item>
///   <item>Validates <c>PaymentId &gt; 0</c>.</item>
///   <item>Calls <see cref="IBillingHistoryQueryService.GetReceiptAsync"/> and maps
///         <c>null</c> (not found / not owned) to 404 — intentionally no IDOR distinction.</item>
/// </list>
/// </para>
///
/// <para>No <c>IBillingDbContext</c>, <c>DbSet</c>, or EF Core references appear in this file.</para>
/// </summary>
public sealed class GetReceiptQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetReceiptQuery, BaseResponse<ReceiptDto>>
{
    private readonly IBillingHistoryQueryService _historyService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetReceiptQueryHandler(
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

    public async Task<BaseResponse<ReceiptDto>> Handle(
        GetReceiptQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // ── parentId is ALWAYS from the JWT — never from the request body ───────
            var parentId = _currentUser.UserId;
            if (!parentId.HasValue || parentId.Value <= 0)
            {
                return Unauthorized<ReceiptDto>(_localizer[SharedResourcesKey.Unauthorized]);
            }

            // ── Basic input guard (queries are not auto-validated) ───────────────────
            if (request.PaymentId <= 0)
            {
                return BadRequest<ReceiptDto>(_localizer[SharedResourcesKey.EmptyIdValidation]);
            }

            // ── Delegate to service — includes IDOR re-check ─────────────────────────
            var receipt = await _historyService.GetReceiptAsync(
                parentId.Value,
                request.PaymentId,
                cancellationToken);

            if (receipt is null)
            {
                // Intentionally vague: covers "not found" AND "not owned by caller" (anti-IDOR).
                return NotFound<ReceiptDto>(_localizer[SharedResourcesKey.ReceiptNotFound]);
            }

            _logger.LogInfo(
                $"GetReceipt: parentId={parentId.Value}, paymentId={request.PaymentId}.");

            return new BaseResponse<ReceiptDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.ReceiptRetrievedSuccessfully],
                Data       = receipt,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error in GetReceiptQuery for parentId={_currentUser.UserId}, paymentId={request.PaymentId}.");
            return ServerError<ReceiptDto>(
                _localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}

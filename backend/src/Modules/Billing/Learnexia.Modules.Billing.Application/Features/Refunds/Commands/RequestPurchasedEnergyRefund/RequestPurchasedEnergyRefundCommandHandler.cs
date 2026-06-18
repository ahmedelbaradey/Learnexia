using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Refunds.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Refunds.Commands.RequestPurchasedEnergyRefund;

/// <summary>
/// Handles <see cref="RequestPurchasedEnergyRefundCommand"/> (P10-17-BE-3).
///
/// <para><strong>Option C — EF-free.</strong> All persistence/provider/ledger logic lives in
/// <see cref="IRefundService"/> and <see cref="IFamilyEnergyQueryService"/>. This handler:
/// <list type="number">
///   <item>Resolves the parent user id from JWT (<c>ICurrentUserService</c>).</item>
///   <item>Resolves the family account id via <c>IFamilyEnergyQueryService</c> + IDOR family-scope guard.</item>
///   <item>Calls <c>IRefundService.ComputeRefundableAsync</c> — rejects if refundable ≤ 0.</item>
///   <item>Calls <c>IRefundService.InitiateProviderRefundAsync</c> — initiate-only.</item>
///   <item>Returns <c>BaseResponse&lt;RefundRequestResultDto&gt;</c>.</item>
/// </list>
/// The actual ledger change is driven by the <c>refund.succeeded</c> webhook (BE-4).</para>
/// </summary>
public sealed class RequestPurchasedEnergyRefundCommandHandler
    : BaseResponseHandler,
      ICommandHandler<RequestPurchasedEnergyRefundCommand, BaseResponse<RefundRequestResultDto>>
{
    private readonly IRefundService _refundService;
    private readonly IFamilyEnergyQueryService _familyEnergyQuery;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public RequestPurchasedEnergyRefundCommandHandler(
        IRefundService refundService,
        IFamilyEnergyQueryService familyEnergyQuery,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _refundService    = refundService;
        _familyEnergyQuery = familyEnergyQuery;
        _currentUser      = currentUser;
        _logger           = logger;
        _localizer        = localizer;
    }

    public async Task<BaseResponse<RefundRequestResultDto>> Handle(
        RequestPurchasedEnergyRefundCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // ── JWT resolution (IDOR guard) ──────────────────────────────────────
            var parentUserId = _currentUser.UserId;
            if (parentUserId is null)
                return Unauthorized<RefundRequestResultDto>(
                    _localizer[SharedResourcesKey.UnauthorizedAccess]);

            // ── Resolve family account (family-scope authz) ──────────────────────
            var overview = await _familyEnergyQuery.GetOverviewAsync(parentUserId.Value, cancellationToken);
            if (overview is null)
                return NotFound<RefundRequestResultDto>(
                    _localizer[SharedResourcesKey.FamilyEnergyWalletNotFound]);

            // ── Compute FIFO refundable quote from the immutable ledger ──────────
            var quote = await _refundService.ComputeRefundableAsync(
                overview.FamilyAccountId,
                request.PurchasePaymentId,
                cancellationToken);

            if (quote is null)
                return NotFound<RefundRequestResultDto>(
                    _localizer[SharedResourcesKey.PaymentNotFound]);

            if (quote.Refundable <= 0)
            {
                _logger.LogInfo(
                    $"RequestPurchasedEnergyRefund: refundable=0 for paymentId={request.PurchasePaymentId}, " +
                    $"parentId={parentUserId}.");
                return new BaseResponse<RefundRequestResultDto>
                {
                    Successed  = false,
                    StatusCode = System.Net.HttpStatusCode.UnprocessableEntity,
                    Message    = _localizer[SharedResourcesKey.RefundableAmountIsZero],
                };
            }

            // ── IDOR: verify the payment belongs to this parent's family ─────────
            // ComputeRefundableAsync already scopes by familyAccountId (which is resolved
            // from the JWT parent — IDOR-safe). A non-null quote with a matching paymentId
            // confirms ownership. Non-owners get PaymentNotFound (null quote) above.

            // ── Initiate provider refund (initiate-only — ledger change via webhook) ─
            var result = await _refundService.InitiateProviderRefundAsync(
                purchasePaymentId : request.PurchasePaymentId,
                refundableAmount  : quote.RefundableAmount,
                reason            : request.Reason,
                actorUserId       : parentUserId.Value,
                ct                : cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogInfo(
                    $"RequestPurchasedEnergyRefund: provider failed for paymentId={request.PurchasePaymentId}, " +
                    $"reason={result.FailureReason}.");

                return result.FailureReason switch
                {
                    InitiateRefundFailureReason.PaymentNotFound =>
                        NotFound<RefundRequestResultDto>(
                            _localizer[SharedResourcesKey.PaymentNotFound]),
                    InitiateRefundFailureReason.PaymentNotSucceeded =>
                        new BaseResponse<RefundRequestResultDto>
                        {
                            Successed  = false,
                            StatusCode = System.Net.HttpStatusCode.UnprocessableEntity,
                            Message    = _localizer[SharedResourcesKey.PaymentNotRefundable],
                        },
                    _ => ServerError<RefundRequestResultDto>(
                        _localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]),
                };
            }

            _logger.LogInfo(
                $"RequestPurchasedEnergyRefund: initiated for paymentId={request.PurchasePaymentId}, " +
                $"parentId={parentUserId}, amount={quote.RefundableAmount}.");

            return new BaseResponse<RefundRequestResultDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.RefundRequestAccepted],
                Data       = new RefundRequestResultDto(
                    request.PurchasePaymentId,
                    _localizer[SharedResourcesKey.RefundRequestAccepted]),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error in RequestPurchasedEnergyRefundCommand for paymentId={request.PurchasePaymentId}.");
            return ServerError<RefundRequestResultDto>(
                _localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}

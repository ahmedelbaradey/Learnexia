using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Refunds.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Refunds.Commands.AdminRequestPurchasedEnergyRefund;

/// <summary>
/// Handles <see cref="AdminRequestPurchasedEnergyRefundCommand"/> (P10-17-BE-6).
///
/// <para><strong>Option C — EF-free.</strong> All persistence/provider/ledger logic lives
/// in <see cref="IRefundService"/>. This handler reuses
/// <c>ComputeRefundableAsync</c> + <c>InitiateProviderRefundAsync</c> — no duplicate logic.
/// </para>
///
/// <para><strong>Admin vs parent differences:</strong>
/// <list type="bullet">
///   <item>No family-scope restriction — admin can refund any family's pack.</item>
///   <item>Family/parent resolved from the target payment — never from the admin JWT.</item>
///   <item>Admin actor id ledgered on the <c>Refund</c> row via <c>SaveChangesAsync</c>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class AdminRequestPurchasedEnergyRefundCommandHandler
    : BaseResponseHandler,
      ICommandHandler<AdminRequestPurchasedEnergyRefundCommand, BaseResponse<RefundRequestResultDto>>
{
    private readonly IRefundService _refundService;
    private readonly IAdminRefundQueryService _adminRefundQuery;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AdminRequestPurchasedEnergyRefundCommandHandler(
        IRefundService refundService,
        IAdminRefundQueryService adminRefundQuery,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _refundService    = refundService;
        _adminRefundQuery = adminRefundQuery;
        _currentUser      = currentUser;
        _logger           = logger;
        _localizer        = localizer;
    }

    public async Task<BaseResponse<RefundRequestResultDto>> Handle(
        AdminRequestPurchasedEnergyRefundCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Admin actor id from JWT (for audit ledger).
            var adminUserId = _currentUser.UserId ?? 0;

            // Resolve the family account id from the target payment (NOT from admin JWT).
            var familyAccountId = await _adminRefundQuery.ResolveFamilyAccountIdByPaymentAsync(
                request.PurchasePaymentId, cancellationToken);

            if (familyAccountId is null)
                return NotFound<RefundRequestResultDto>(
                    _localizer[SharedResourcesKey.PaymentNotFound]);

            // Compute FIFO refundable quote (reuses BE-1/BE-2 reconciliation).
            var quote = await _refundService.ComputeRefundableAsync(
                familyAccountId.Value,
                request.PurchasePaymentId,
                cancellationToken);

            if (quote is null)
                return NotFound<RefundRequestResultDto>(
                    _localizer[SharedResourcesKey.PaymentNotFound]);

            if (quote.Refundable <= 0)
            {
                _logger.LogInfo(
                    $"AdminRequestPurchasedEnergyRefund: refundable=0 for paymentId={request.PurchasePaymentId}.");
                return new BaseResponse<RefundRequestResultDto>
                {
                    Successed  = false,
                    StatusCode = System.Net.HttpStatusCode.UnprocessableEntity,
                    Message    = _localizer[SharedResourcesKey.RefundableAmountIsZero],
                };
            }

            // Initiate provider refund (initiate-only — ledger change via webhook BE-4).
            var result = await _refundService.InitiateProviderRefundAsync(
                purchasePaymentId : request.PurchasePaymentId,
                refundableAmount  : quote.RefundableAmount,
                reason            : request.Reason,
                actorUserId       : adminUserId,
                ct                : cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogInfo(
                    $"AdminRequestPurchasedEnergyRefund: provider failed for paymentId={request.PurchasePaymentId}, " +
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
                $"AdminRequestPurchasedEnergyRefund: admin={adminUserId} initiated refund for " +
                $"paymentId={request.PurchasePaymentId}, amount={quote.RefundableAmount}.");

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
                $"Error in AdminRequestPurchasedEnergyRefundCommand for paymentId={request.PurchasePaymentId}.");
            return ServerError<RefundRequestResultDto>(
                _localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}

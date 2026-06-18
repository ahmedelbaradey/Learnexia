using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Refunds.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Refunds.Queries.GetRefundableQuote;

/// <summary>
/// Handles <see cref="GetAdminRefundableQuoteQuery"/> (P10-17-BE-6).
///
/// <para><strong>Option C — EF-free.</strong> Reuses <c>IRefundService.ComputeRefundableAsync</c>
/// and <c>IAdminRefundQueryService.ResolveFamilyAccountIdByPaymentAsync</c>.</para>
/// </summary>
public sealed class GetAdminRefundableQuoteQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetAdminRefundableQuoteQuery, BaseResponse<RefundableQuoteDto>>
{
    private readonly IRefundService _refundService;
    private readonly IAdminRefundQueryService _adminRefundQuery;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetAdminRefundableQuoteQueryHandler(
        IRefundService refundService,
        IAdminRefundQueryService adminRefundQuery,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _refundService    = refundService;
        _adminRefundQuery = adminRefundQuery;
        _logger           = logger;
        _localizer        = localizer;
    }

    public async Task<BaseResponse<RefundableQuoteDto>> Handle(
        GetAdminRefundableQuoteQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.PurchasePaymentId <= 0)
                return BadRequest<RefundableQuoteDto>(
                    _localizer[SharedResourcesKey.EmptyIdValidation]);

            // Resolve the family account from the payment (NOT family-scoped — admin).
            var familyAccountId = await _adminRefundQuery.ResolveFamilyAccountIdByPaymentAsync(
                request.PurchasePaymentId, cancellationToken);

            if (familyAccountId is null)
                return NotFound<RefundableQuoteDto>(
                    _localizer[SharedResourcesKey.PaymentNotFound]);

            var quote = await _refundService.ComputeRefundableAsync(
                familyAccountId.Value,
                request.PurchasePaymentId,
                cancellationToken);

            if (quote is null)
                return NotFound<RefundableQuoteDto>(
                    _localizer[SharedResourcesKey.PaymentNotFound]);

            _logger.LogInfo(
                $"GetAdminRefundableQuote: paymentId={request.PurchasePaymentId}, " +
                $"refundable={quote.Refundable}.");

            return new BaseResponse<RefundableQuoteDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.RefundableQuoteRetrieved],
                Data       = quote,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error in GetAdminRefundableQuoteQuery for paymentId={request.PurchasePaymentId}.");
            return ServerError<RefundableQuoteDto>(
                _localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}

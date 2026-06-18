using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Refunds.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Refunds.Queries.GetRefundableQuote;

/// <summary>
/// Handles <see cref="GetRefundableQuoteQuery"/> (P10-17-BE-5).
///
/// <para><strong>Option C — EF-free.</strong> All persistence/ledger logic lives in
/// <see cref="IRefundService.ComputeRefundableAsync"/>. This handler:
/// <list type="number">
///   <item>Resolves the parent user id from JWT.</item>
///   <item>Resolves the family account via <c>IFamilyEnergyQueryService</c> + IDOR guard.</item>
///   <item>Calls <c>IRefundService.ComputeRefundableAsync</c> and returns the quote.</item>
/// </list>
/// </para>
/// </summary>
public sealed class GetRefundableQuoteQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetRefundableQuoteQuery, BaseResponse<RefundableQuoteDto>>
{
    private readonly IRefundService _refundService;
    private readonly IFamilyEnergyQueryService _familyEnergyQuery;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetRefundableQuoteQueryHandler(
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

    public async Task<BaseResponse<RefundableQuoteDto>> Handle(
        GetRefundableQuoteQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.PurchasePaymentId <= 0)
                return BadRequest<RefundableQuoteDto>(
                    _localizer[SharedResourcesKey.EmptyIdValidation]);

            // IDOR guard: parent from JWT.
            var parentUserId = _currentUser.UserId;
            if (parentUserId is null)
                return Unauthorized<RefundableQuoteDto>(
                    _localizer[SharedResourcesKey.UnauthorizedAccess]);

            // Resolve family account (family-scope guard).
            var overview = await _familyEnergyQuery.GetOverviewAsync(parentUserId.Value, cancellationToken);
            if (overview is null)
                return NotFound<RefundableQuoteDto>(
                    _localizer[SharedResourcesKey.FamilyEnergyWalletNotFound]);

            // Compute FIFO refundable quote.
            var quote = await _refundService.ComputeRefundableAsync(
                overview.FamilyAccountId,
                request.PurchasePaymentId,
                cancellationToken);

            if (quote is null)
                return NotFound<RefundableQuoteDto>(
                    _localizer[SharedResourcesKey.PaymentNotFound]);

            _logger.LogInfo(
                $"GetRefundableQuote: paymentId={request.PurchasePaymentId}, " +
                $"refundable={quote.Refundable}, parentId={parentUserId}.");

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
                $"Error in GetRefundableQuoteQuery for paymentId={request.PurchasePaymentId}.");
            return ServerError<RefundableQuoteDto>(
                _localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}

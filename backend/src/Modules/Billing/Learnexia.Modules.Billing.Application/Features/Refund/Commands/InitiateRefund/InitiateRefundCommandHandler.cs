using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Refund.Commands.InitiateRefund;

/// <summary>
/// Handles <see cref="InitiateRefundCommand"/>.
///
/// <para><strong>Option C — handler is EF-free.</strong> All persistence and provider-call logic
/// lives in <see cref="IRefundService"/>. This handler only:
/// <list type="number">
///   <item>Resolves the admin's <c>userId</c> from the JWT.</item>
///   <item>Calls <see cref="IRefundService.InitiateRefundAsync"/>.</item>
///   <item>Maps the typed result to <see cref="BaseResponse{T}"/>.</item>
/// </list>
/// </para>
///
/// <para>No <c>IBillingDbContext</c>, <c>DbSet</c>, or EF Core references appear in this file.</para>
/// </summary>
public sealed class InitiateRefundCommandHandler
    : BaseResponseHandler,
      ICommandHandler<InitiateRefundCommand, BaseResponse<InitiateRefundResultDto>>
{
    private readonly IRefundService _refundService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public InitiateRefundCommandHandler(
        IRefundService refundService,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _refundService = refundService;
        _currentUser   = currentUser;
        _logger        = logger;
        _localizer     = localizer;
    }

    public async Task<BaseResponse<InitiateRefundResultDto>> Handle(
        InitiateRefundCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // ── Admin userId from JWT ────────────────────────────────────────────────
            var adminUserId = _currentUser.UserId ?? 0;

            // ── Delegate all EF / provider / audit logic to the service ──────────────
            var result = await _refundService.InitiateRefundAsync(
                request.PaymentId,
                request.Reason,
                adminUserId,
                cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogInfo(
                    $"InitiateRefund: failed for paymentId={request.PaymentId}, reason={result.FailureReason}.");

                return result.FailureReason switch
                {
                    InitiateRefundFailureReason.PaymentNotFound =>
                        NotFound<InitiateRefundResultDto>(
                            _localizer[SharedResourcesKey.PaymentNotFound]),

                    InitiateRefundFailureReason.PaymentNotSucceeded =>
                        new BaseResponse<InitiateRefundResultDto>
                        {
                            Successed  = false,
                            StatusCode = System.Net.HttpStatusCode.UnprocessableEntity,
                            Message    = _localizer[SharedResourcesKey.PaymentNotRefundable],
                        },

                    _ => ServerError<InitiateRefundResultDto>(
                        _localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]),
                };
            }

            _logger.LogInfo(
                $"InitiateRefund: succeeded for paymentId={request.PaymentId}, adminId={adminUserId}.");

            return new BaseResponse<InitiateRefundResultDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.RefundInitiatedSuccessfully],
                Data       = new InitiateRefundResultDto(
                    request.PaymentId,
                    _localizer[SharedResourcesKey.RefundInitiatedSuccessfully]),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error in InitiateRefundCommand for paymentId={request.PaymentId}.");
            return ServerError<InitiateRefundResultDto>(
                _localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}

using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Seats.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Seats.Commands.StartSeatCheckout;

/// <summary>
/// Handles <see cref="StartSeatCheckoutCommand"/> (P10-14-BE-6).
///
/// <para><strong>Option C — handler is EF-free.</strong> All persistence, provider calls, and
/// transaction logic live in <see cref="ISeatService"/>. This handler only:
/// <list type="number">
///   <item>Resolves <c>parentId</c> from the JWT via <see cref="ICurrentUserService"/>.</item>
///   <item>Builds a per-request idempotency key.</item>
///   <item>Delegates all business + EF logic to <see cref="ISeatService.StartSeatCheckoutAsync"/>.</item>
///   <item>Maps the service result to <see cref="BaseResponse{T}"/>.</item>
/// </list>
/// </para>
///
/// <para>No <c>IBillingDbContext</c>, <c>DbSet</c>, or EF Core references appear in this file.</para>
/// </summary>
public sealed class StartSeatCheckoutCommandHandler
    : BaseResponseHandler,
      ICommandHandler<StartSeatCheckoutCommand, BaseResponse<SeatCheckoutResultDto>>
{
    private readonly ISeatService _seatService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public StartSeatCheckoutCommandHandler(
        ISeatService seatService,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _seatService  = seatService;
        _currentUser  = currentUser;
        _logger       = logger;
        _localizer    = localizer;
    }

    public async Task<BaseResponse<SeatCheckoutResultDto>> Handle(
        StartSeatCheckoutCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // ── parentId is ALWAYS from the JWT, never the request body ────────────────
            var parentId = _currentUser.UserId
                ?? throw new InvalidOperationException("Parent JWT claim missing.");

            // ── Generate a per-attempt idempotency key ─────────────────────────────────
            var idempotencyKey = $"seat-checkout:{parentId}:{request.SeatQuantity}:{Guid.NewGuid():N}";

            // ── Delegate all EF / provider / transaction work to the service ───────────
            var result = await _seatService.StartSeatCheckoutAsync(
                parentId,
                request.SeatQuantity,
                idempotencyKey,
                cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogInfo(
                    $"StartSeatCheckout: failed for parentId={parentId}, qty={request.SeatQuantity}, reason={result.FailureReason}.");

                return result.FailureReason switch
                {
                    SeatCheckoutFailureReason.FreePlanNotAllowed =>
                        new BaseResponse<SeatCheckoutResultDto>
                        {
                            Successed  = false,
                            StatusCode = System.Net.HttpStatusCode.Forbidden,
                            Message    = _localizer[SharedResourcesKey.SeatCheckoutFreePlanNotAllowed],
                        },

                    SeatCheckoutFailureReason.ExceedsMaxSeats =>
                        new BaseResponse<SeatCheckoutResultDto>
                        {
                            Successed  = false,
                            StatusCode = System.Net.HttpStatusCode.Conflict,
                            Message    = _localizer[SharedResourcesKey.SeatCheckoutExceedsMaxSeats],
                        },

                    SeatCheckoutFailureReason.NoActiveSubscription =>
                        new BaseResponse<SeatCheckoutResultDto>
                        {
                            Successed  = false,
                            StatusCode = System.Net.HttpStatusCode.UnprocessableEntity,
                            Message    = _localizer[SharedResourcesKey.SubscriptionNotFound],
                        },

                    SeatCheckoutFailureReason.ProviderUnavailable =>
                        new BaseResponse<SeatCheckoutResultDto>
                        {
                            Successed  = false,
                            StatusCode = System.Net.HttpStatusCode.ServiceUnavailable,
                            Message    = _localizer[SharedResourcesKey.PaymentProviderUnavailable],
                        },

                    _ => ServerError<SeatCheckoutResultDto>(
                        _localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]),
                };
            }

            _logger.LogInfo(
                $"StartSeatCheckout: paymentId={result.PaymentId}, parentId={parentId}, qty={request.SeatQuantity}.");

            return new BaseResponse<SeatCheckoutResultDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.SeatCheckoutSessionCreated],
                Data       = new SeatCheckoutResultDto(
                    PaymentId         : result.PaymentId,
                    RedirectUrl       : result.RedirectUrl!,
                    ProviderPaymentRef: result.ProviderPaymentRef!,
                    Amount            : result.Amount),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error in StartSeatCheckoutCommand for parentUserId={_currentUser.UserId}.");
            return ServerError<SeatCheckoutResultDto>(
                _localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}

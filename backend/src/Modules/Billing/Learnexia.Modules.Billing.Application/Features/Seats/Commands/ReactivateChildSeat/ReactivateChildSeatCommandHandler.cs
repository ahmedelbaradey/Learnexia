using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Seats.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Seats.Commands.ReactivateChildSeat;

/// <summary>
/// Handles <see cref="ReactivateChildSeatCommand"/> (P10-15-BE-6).
///
/// <para><strong>Option C — handler is EF-free.</strong> All persistence and transaction logic live
/// in <see cref="ISeatService.StartSeatReactivationCheckoutAsync"/>. This handler resolves the parent
/// identity from the JWT and delegates.</para>
/// </summary>
public sealed class ReactivateChildSeatCommandHandler
    : BaseResponseHandler,
      ICommandHandler<ReactivateChildSeatCommand, BaseResponse<SeatCheckoutResultDto>>
{
    private readonly ISeatService _seatService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ReactivateChildSeatCommandHandler(
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
        ReactivateChildSeatCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var parentId = _currentUser.UserId
                ?? throw new InvalidOperationException("Parent JWT claim missing.");

            var result = await _seatService.StartSeatReactivationCheckoutAsync(
                parentId,
                request.ChildId,
                request.IdempotencyKey,
                cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogInfo(
                    $"ReactivateChildSeat: failed for parentId={parentId}, childId={request.ChildId}, reason={result.FailureReason}.");

                return result.FailureReason switch
                {
                    SeatCheckoutFailureReason.FreePlanNotAllowed =>
                        new BaseResponse<SeatCheckoutResultDto>
                        {
                            Successed  = false,
                            StatusCode = System.Net.HttpStatusCode.Conflict,
                            Message    = _localizer[SharedResourcesKey.SeatCheckoutFreePlanNotAllowed],
                        },
                    SeatCheckoutFailureReason.NoActiveSubscription =>
                        new BaseResponse<SeatCheckoutResultDto>
                        {
                            Successed  = false,
                            StatusCode = System.Net.HttpStatusCode.UnprocessableEntity,
                            Message    = _localizer[SharedResourcesKey.SubscriptionNotFound],
                        },
                    SeatCheckoutFailureReason.ProviderUnavailable =>
                        ServerError<SeatCheckoutResultDto>(
                            _localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]),
                    _ => ServerError<SeatCheckoutResultDto>(
                            _localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]),
                };
            }

            _logger.LogInfo(
                $"ReactivateChildSeat: checkout initiated for parentId={parentId}, childId={request.ChildId}, " +
                $"paymentId={result.PaymentId}, amount={result.Amount}.");

            return new BaseResponse<SeatCheckoutResultDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.SeatReactivateInitiated],
                Data       = new SeatCheckoutResultDto(
                    result.PaymentId,
                    result.RedirectUrl!,
                    result.ProviderPaymentRef!,
                    result.Amount),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in ReactivateChildSeatCommand for parentId={_currentUser.UserId}.");
            return ServerError<SeatCheckoutResultDto>(
                _localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}

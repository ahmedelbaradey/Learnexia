using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Seats.Commands.CancelExtraSeat;

/// <summary>
/// Handles <see cref="CancelExtraSeatCommand"/> (P10-14-BE-8).
///
/// <para><strong>Option C — handler is EF-free.</strong> All persistence and transaction logic
/// live in <see cref="ISeatService"/>. This handler only resolves the parent identity from the
/// JWT and delegates to <see cref="ISeatService.ScheduleExtraSeatCancellationAsync"/>.</para>
///
/// <para><strong>Effect (P10-14-BE-8, cycle-end cancel):</strong> records a scheduled removal at the
/// next renewal boundary. It does NOT start a grace period (grace = payment-failure only), does NOT
/// reduce the active-seat count mid-cycle, and does NOT reclaim energy or delete a child. The seat
/// stays Active for the rest of the cycle; P10-15 applies the removal + NoSeat/Locked at renewal.</para>
/// </summary>
public sealed class CancelExtraSeatCommandHandler
    : BaseResponseHandler,
      ICommandHandler<CancelExtraSeatCommand, BaseResponse<bool>>
{
    private readonly ISeatService _seatService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public CancelExtraSeatCommandHandler(
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

    public async Task<BaseResponse<bool>> Handle(
        CancelExtraSeatCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var parentId = _currentUser.UserId
                ?? throw new InvalidOperationException("Parent JWT claim missing.");

            var result = await _seatService.ScheduleExtraSeatCancellationAsync(
                parentId,
                request.SeatQuantity,
                cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogInfo(
                    $"CancelExtraSeat: failed for parentId={parentId}, qty={request.SeatQuantity}, reason={result.FailureReason}.");

                return result.FailureReason switch
                {
                    "InsufficientExtraSeats" =>
                        new BaseResponse<bool>
                        {
                            Successed  = false,
                            StatusCode = System.Net.HttpStatusCode.Conflict,
                            Message    = _localizer[SharedResourcesKey.SeatCancelInsufficientExtraSeats],
                        },

                    "SubscriptionNotFound" =>
                        new BaseResponse<bool>
                        {
                            Successed  = false,
                            StatusCode = System.Net.HttpStatusCode.UnprocessableEntity,
                            Message    = _localizer[SharedResourcesKey.SubscriptionNotFound],
                        },

                    _ => ServerError<bool>(
                        _localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]),
                };
            }

            _logger.LogInfo(
                $"CancelExtraSeat: cycle-end cancellation scheduled for parentId={parentId}, qty={request.SeatQuantity}.");

            return new BaseResponse<bool>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.SeatCancelledSuccessfully],
                Data       = true,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error in CancelExtraSeatCommand for parentUserId={_currentUser.UserId}.");
            return ServerError<bool>(
                _localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}

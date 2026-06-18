using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Seats.Commands.ChooseActiveChildren;

/// <summary>
/// Handles <see cref="ChooseActiveChildrenCommand"/> (P10-15-BE-6).
///
/// <para><strong>Option C — handler is EF-free.</strong> All persistence and transaction logic live
/// in <see cref="ISeatService.ChooseActiveChildrenAsync"/>. This handler resolves the parent identity
/// from the JWT and delegates.</para>
/// </summary>
public sealed class ChooseActiveChildrenCommandHandler
    : BaseResponseHandler,
      ICommandHandler<ChooseActiveChildrenCommand, BaseResponse<bool>>
{
    private readonly ISeatService _seatService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ChooseActiveChildrenCommandHandler(
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
        ChooseActiveChildrenCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var parentId = _currentUser.UserId
                ?? throw new InvalidOperationException("Parent JWT claim missing.");

            var result = await _seatService.ChooseActiveChildrenAsync(
                parentId,
                request.ActiveChildIds,
                cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogInfo(
                    $"ChooseActiveChildren: failed for parentId={parentId}, reason={result.FailureReason}.");

                return result.FailureReason switch
                {
                    SeatChoiceFailureReason.ExceedsPaidSeats =>
                        new BaseResponse<bool>
                        {
                            Successed  = false,
                            StatusCode = System.Net.HttpStatusCode.UnprocessableEntity,
                            Message    = _localizer[SharedResourcesKey.SeatChoiceExceedsLimit],
                        },
                    SeatChoiceFailureReason.ChildNotInFamily =>
                        new BaseResponse<bool>
                        {
                            Successed  = false,
                            StatusCode = System.Net.HttpStatusCode.Forbidden,
                            Message    = _localizer[SharedResourcesKey.ChildNotInFamily],
                        },
                    SeatChoiceFailureReason.LockedChildRequiresReactivation =>
                        new BaseResponse<bool>
                        {
                            Successed  = false,
                            StatusCode = System.Net.HttpStatusCode.Conflict,
                            Message    = _localizer[SharedResourcesKey.ChildSeatLockedNoEnergy],
                        },
                    _ => ServerError<bool>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]),
                };
            }

            _logger.LogInfo(
                $"ChooseActiveChildren: applied for parentId={parentId}, changed={result.ChangedCount}.");

            return new BaseResponse<bool>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.SeatChoiceApplied],
                Data       = true,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in ChooseActiveChildrenCommand for parentId={_currentUser.UserId}.");
            return ServerError<bool>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}

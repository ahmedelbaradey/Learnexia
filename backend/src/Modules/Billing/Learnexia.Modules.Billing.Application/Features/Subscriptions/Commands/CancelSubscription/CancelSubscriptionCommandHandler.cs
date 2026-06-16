using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Subscriptions.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Subscriptions.Commands.CancelSubscription;

/// <summary>
/// Thin handler (Option C): delegates all EF / transaction / provider-cancel logic to
/// <see cref="ISubscriptionService.CancelAsync"/>. Stays EF-free.
/// </summary>
public class CancelSubscriptionCommandHandler
    : BaseResponseHandler, ICommandHandler<CancelSubscriptionCommand, BaseResponse<SubscriptionDto>>
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILoggerManager _logger;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public CancelSubscriptionCommandHandler(
        ISubscriptionService subscriptionService,
        ILoggerManager logger,
        ICurrentUserService currentUser,
        IStringLocalizer<SharedResources> localizer)
    {
        _subscriptionService = subscriptionService;
        _logger              = logger;
        _currentUser         = currentUser;
        _localizer           = localizer;
    }

    public async Task<BaseResponse<SubscriptionDto>> Handle(
        CancelSubscriptionCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _subscriptionService.CancelAsync(
                parentUserId : request.ParentUserId,
                actorUserId  : _currentUser.UserId ?? 0,
                ct           : cancellationToken);

            if (result.FailureReason == SubscriptionOperationFailureReason.SubscriptionNotFound)
            {
                return new BaseResponse<SubscriptionDto>
                {
                    Successed  = false,
                    StatusCode = System.Net.HttpStatusCode.NotFound,
                    Message    = _localizer[SharedResourcesKey.SubscriptionNotFound],
                };
            }

            return new BaseResponse<SubscriptionDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.SubscriptionCanceledSuccessfully],
                Data       = result.Data,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in CancelSubscriptionCommand for parentUserId={request.ParentUserId}");
            return ServerError<SubscriptionDto>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}

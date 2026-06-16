using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Subscriptions.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Subscriptions.Commands.RequestUpgrade;

/// <summary>
/// Thin handler (Option C): delegates all EF / transaction / state-machine logic to
/// <see cref="ISubscriptionService.RequestUpgradeAsync"/>. Stays EF-free.
/// </summary>
public class RequestUpgradeCommandHandler
    : BaseResponseHandler, ICommandHandler<RequestUpgradeCommand, BaseResponse<SubscriptionDto>>
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILoggerManager _logger;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public RequestUpgradeCommandHandler(
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
        RequestUpgradeCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _subscriptionService.RequestUpgradeAsync(
                parentUserId  : request.ParentUserId,
                billingPeriod : request.BillingPeriod,
                actorUserId   : _currentUser.UserId ?? 0,
                ct            : cancellationToken);

            _logger.LogInfo($"RequestUpgrade: parentId={request.ParentUserId} → PendingPayment, period={request.BillingPeriod}.");

            return new BaseResponse<SubscriptionDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.SubscriptionUpgradeRequested],
                Data       = result.Data,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in RequestUpgradeCommand for parentUserId={request.ParentUserId}");
            return ServerError<SubscriptionDto>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}

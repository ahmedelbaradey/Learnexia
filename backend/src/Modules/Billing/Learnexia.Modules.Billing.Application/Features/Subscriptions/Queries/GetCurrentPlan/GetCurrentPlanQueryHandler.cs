using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Subscriptions.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Subscriptions.Queries.GetCurrentPlan;

/// <summary>
/// Thin handler (Option C): delegates all EF / GlobalSettings / Plans-table access to
/// <see cref="ISubscriptionService.GetCurrentPlanAsync"/>. Stays EF-free.
/// </summary>
public class GetCurrentPlanQueryHandler
    : BaseResponseHandler, IQueryHandler<GetCurrentPlanQuery, BaseResponse<SubscriptionDto>>
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetCurrentPlanQueryHandler(
        ISubscriptionService subscriptionService,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _subscriptionService = subscriptionService;
        _logger              = logger;
        _localizer           = localizer;
    }

    public async Task<BaseResponse<SubscriptionDto>> Handle(
        GetCurrentPlanQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var dto = await _subscriptionService.GetCurrentPlanAsync(request.ParentUserId, cancellationToken);

            return new BaseResponse<SubscriptionDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.SubscriptionRetrievedSuccessfully],
                Data       = dto,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in GetCurrentPlanQuery for parentUserId={request.ParentUserId}");
            return ServerError<SubscriptionDto>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}

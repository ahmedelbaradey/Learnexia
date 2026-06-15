using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Subscriptions.Dtos;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Subscriptions.Queries.GetCurrentPlan;

public class GetCurrentPlanQueryHandler
    : BaseResponseHandler, IQueryHandler<GetCurrentPlanQuery, BaseResponse<SubscriptionDto>>
{
    private readonly IBillingDbContext _db;
    private readonly IGlobalSettingsProvider _settings;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetCurrentPlanQueryHandler(
        IBillingDbContext db,
        IGlobalSettingsProvider settings,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _db = db;
        _settings = settings;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<SubscriptionDto>> Handle(
        GetCurrentPlanQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Resolve or synthesise a Free subscription if the parent has never had one.
            var subscription = await _db.Subscriptions
                .AsNoTracking()
                .Where(s => s.ParentUserId == request.ParentUserId && s.Status == SubscriptionStatus.Active)
                .FirstOrDefaultAsync(cancellationToken);

            // Parents without a subscription row are on Free (row may not exist yet).
            var planCode = subscription?.PlanCode ?? PlanCode.Free;
            var billingPeriod = subscription?.BillingPeriod ?? BillingPeriod.Monthly;
            var status = subscription?.Status ?? SubscriptionStatus.Active;

            // Resolve benefit amounts from GlobalSettings (never hard-coded).
            var (monthlyCredits, dailyCap) = planCode == PlanCode.Premium
                ? (_settings.GetInt(GlobalSettingKeys.PremiumMonthlyCredits, 5000),
                   _settings.GetInt(GlobalSettingKeys.PremiumDailyCap, 250))
                : (_settings.GetInt(GlobalSettingKeys.FreeMonthlyCredits, 100),
                   _settings.GetInt(GlobalSettingKeys.FreeDailyCap, 10));

            // Resolve plan name from the Plans table (fallback = code string).
            var planName = await _db.Plans
                .AsNoTracking()
                .Where(p => p.Code == planCode)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(cancellationToken)
                ?? planCode.ToString();

            var dto = new SubscriptionDto
            {
                Id = subscription?.Id ?? 0,
                PlanCode = planCode,
                PlanName = planName,
                BillingPeriod = billingPeriod,
                Status = status,
                CurrentCycleStart = subscription?.CurrentCycleStart,
                CurrentCycleEnd = subscription?.CurrentCycleEnd,
                PendingPlanCode = subscription?.PendingPlanCode,
                PendingBillingPeriod = subscription?.PendingBillingPeriod,
                MonthlyCredits = monthlyCredits,
                DailySoftCap = dailyCap,
            };

            return new BaseResponse<SubscriptionDto>
            {
                Successed = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message = _localizer[SharedResourcesKey.SubscriptionRetrievedSuccessfully],
                Data = dto,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in GetCurrentPlanQuery for parentUserId={request.ParentUserId}");
            return ServerError<SubscriptionDto>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}

using Learnexia.Modules.Billing.Application.Features.Subscriptions.Dtos;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Subscriptions.Queries.GetPlanComparison;

public class GetPlanComparisonQueryHandler
    : BaseResponseHandler, IQueryHandler<GetPlanComparisonQuery, BaseResponse<PlanComparisonDto>>
{
    private readonly IGlobalSettingsProvider _settings;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetPlanComparisonQueryHandler(
        IGlobalSettingsProvider settings,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _settings = settings;
        _logger = logger;
        _localizer = localizer;
    }

    public Task<BaseResponse<PlanComparisonDto>> Handle(
        GetPlanComparisonQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // All values from GlobalSettings — no hard-coded numbers.
            var freeMonthly   = _settings.GetInt(GlobalSettingKeys.FreeMonthlyCredits, 100);
            var freeDailyCap  = _settings.GetInt(GlobalSettingKeys.FreeDailyCap, 10);
            var premMonthly   = _settings.GetInt(GlobalSettingKeys.PremiumMonthlyCredits, 5000);
            var premDailyCap  = _settings.GetInt(GlobalSettingKeys.PremiumDailyCap, 250);
            var monthlyPrice  = _settings.GetDecimal(GlobalSettingKeys.SubscriptionMonthlyPriceEgp, 199m);
            var annualPrice   = _settings.GetDecimal(GlobalSettingKeys.SubscriptionAnnualPriceEgp, 1990m);

            var dto = new PlanComparisonDto
            {
                Free = new PlanOptionDto
                {
                    Code = "Free",
                    Name = "Free",
                    MonthlyCredits = freeMonthly,
                    DailySoftCap   = freeDailyCap,
                    MonthlyPriceEgp = 0m,
                    AnnualPriceEgp  = 0m,
                },
                Premium = new PlanOptionDto
                {
                    Code = "Premium",
                    Name = "Premium",
                    MonthlyCredits  = premMonthly,
                    DailySoftCap    = premDailyCap,
                    MonthlyPriceEgp = monthlyPrice,
                    AnnualPriceEgp  = annualPrice,
                },
            };

            return Task.FromResult(new BaseResponse<PlanComparisonDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.PlanComparisonRetrievedSuccessfully],
                Data       = dto,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetPlanComparisonQuery");
            return Task.FromResult(ServerError<PlanComparisonDto>(
                _localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]));
        }
    }
}

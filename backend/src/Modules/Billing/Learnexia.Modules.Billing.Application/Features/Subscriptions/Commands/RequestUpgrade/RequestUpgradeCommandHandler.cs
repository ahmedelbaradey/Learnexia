using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Subscriptions.Dtos;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Subscriptions.Commands.RequestUpgrade;

/// <summary>
/// Transitions the parent's subscription to <see cref="SubscriptionStatus.PendingPayment"/>.
///
/// <para>Business rules:
/// <list type="bullet">
///   <item>If no subscription row exists, one is created (first-time upgrade from implicit Free).</item>
///   <item>If already <c>PendingPayment</c> or <c>Active Premium</c>, the billing-period preference
///         is updated and the row is left as is (idempotent).</item>
///   <item>If already <c>Canceled</c> or <c>Downgrading</c>, a new <c>PendingPayment</c> row is
///         created (re-subscribe flow).</item>
/// </list>
/// </para>
/// </summary>
public class RequestUpgradeCommandHandler
    : BaseResponseHandler, ICommandHandler<RequestUpgradeCommand, BaseResponse<SubscriptionDto>>
{
    private readonly IBillingDbContext _db;
    private readonly IGlobalSettingsProvider _settings;
    private readonly ILoggerManager _logger;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public RequestUpgradeCommandHandler(
        IBillingDbContext db,
        IGlobalSettingsProvider settings,
        ILoggerManager logger,
        ICurrentUserService currentUser,
        IStringLocalizer<SharedResources> localizer)
    {
        _db = db;
        _settings = settings;
        _logger = logger;
        _currentUser = currentUser;
        _localizer = localizer;
    }

    public async Task<BaseResponse<SubscriptionDto>> Handle(
        RequestUpgradeCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

            var subscription = await _db.Subscriptions
                .Where(s => s.ParentUserId == request.ParentUserId
                         && s.Status == SubscriptionStatus.Active)
                .FirstOrDefaultAsync(cancellationToken);

            if (subscription is null)
            {
                // No active subscription row — create one in PendingPayment.
                subscription = new Subscription
                {
                    ParentUserId = request.ParentUserId,
                    PlanCode     = PlanCode.Free,        // will become Premium after payment
                    BillingPeriod = request.BillingPeriod,
                    Status        = SubscriptionStatus.PendingPayment,
                };
                await _db.Subscriptions.AddAsync(subscription, cancellationToken);
            }
            else if (subscription.PlanCode == PlanCode.Premium
                     && subscription.Status == SubscriptionStatus.Active)
            {
                // Already Premium Active — record the cadence preference as a pending period change.
                subscription.PendingBillingPeriod = request.BillingPeriod;
            }
            else
            {
                // Free/Downgrading → upgrade intent.
                subscription.BillingPeriod = request.BillingPeriod;
                subscription.Status        = SubscriptionStatus.PendingPayment;
            }

            await _db.SaveChangesAsync(_currentUser.UserId ?? 0);
            await tx.CommitAsync(cancellationToken);

            _logger.LogInfo($"RequestUpgrade: parentId={request.ParentUserId} → PendingPayment, period={request.BillingPeriod}.");

            var dto = BuildDto(subscription);
            return new BaseResponse<SubscriptionDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.SubscriptionUpgradeRequested],
                Data       = dto,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in RequestUpgradeCommand for parentUserId={request.ParentUserId}");
            return ServerError<SubscriptionDto>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }

    private SubscriptionDto BuildDto(Subscription s)
    {
        var planCode  = s.PlanCode;
        var (monthly, daily) = planCode == PlanCode.Premium
            ? (_settings.GetInt(GlobalSettingKeys.PremiumMonthlyCredits, 5000),
               _settings.GetInt(GlobalSettingKeys.PremiumDailyCap, 250))
            : (_settings.GetInt(GlobalSettingKeys.FreeMonthlyCredits, 100),
               _settings.GetInt(GlobalSettingKeys.FreeDailyCap, 10));

        return new SubscriptionDto
        {
            Id                  = s.Id,
            PlanCode            = s.PlanCode,
            PlanName            = s.PlanCode.ToString(),
            BillingPeriod       = s.BillingPeriod,
            Status              = s.Status,
            CurrentCycleStart   = s.CurrentCycleStart,
            CurrentCycleEnd     = s.CurrentCycleEnd,
            PendingPlanCode     = s.PendingPlanCode,
            PendingBillingPeriod = s.PendingBillingPeriod,
            MonthlyCredits      = monthly,
            DailySoftCap        = daily,
        };
    }
}

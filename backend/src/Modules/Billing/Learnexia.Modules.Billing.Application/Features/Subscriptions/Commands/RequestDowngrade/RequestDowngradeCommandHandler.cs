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

namespace Learnexia.Modules.Billing.Application.Features.Subscriptions.Commands.RequestDowngrade;

/// <summary>
/// Transitions the parent's Active Premium subscription to <see cref="SubscriptionStatus.Downgrading"/>.
/// Sets <c>PendingPlanCode = Free</c> to take effect at cycle end.
///
/// <para>IDOR guard: <c>ParentUserId</c> comes from JWT (controller), never from the request body.</para>
/// </summary>
public class RequestDowngradeCommandHandler
    : BaseResponseHandler, ICommandHandler<RequestDowngradeCommand, BaseResponse<SubscriptionDto>>
{
    private readonly IBillingDbContext _db;
    private readonly IGlobalSettingsProvider _settings;
    private readonly ILoggerManager _logger;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public RequestDowngradeCommandHandler(
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
        RequestDowngradeCommand request,
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
                await tx.RollbackAsync(cancellationToken);
                return new BaseResponse<SubscriptionDto>
                {
                    Successed  = false,
                    StatusCode = System.Net.HttpStatusCode.NotFound,
                    Message    = _localizer[SharedResourcesKey.SubscriptionNotFound],
                };
            }

            if (subscription.PlanCode == PlanCode.Free)
            {
                await tx.RollbackAsync(cancellationToken);
                return new BaseResponse<SubscriptionDto>
                {
                    Successed  = false,
                    StatusCode = System.Net.HttpStatusCode.Conflict,
                    Message    = _localizer[SharedResourcesKey.SubscriptionAlreadyFree],
                };
            }

            subscription.Status          = SubscriptionStatus.Downgrading;
            subscription.PendingPlanCode = PlanCode.Free;

            await _db.SaveChangesAsync(_currentUser.UserId ?? 0);
            await tx.CommitAsync(cancellationToken);

            _logger.LogInfo($"RequestDowngrade: parentId={request.ParentUserId} → Downgrading.");

            return new BaseResponse<SubscriptionDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.SubscriptionDowngradeScheduled],
                Data       = BuildDto(subscription),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in RequestDowngradeCommand for parentUserId={request.ParentUserId}");
            return ServerError<SubscriptionDto>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }

    private SubscriptionDto BuildDto(Domain.Entities.Subscription s)
    {
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
            MonthlyCredits      = _settings.GetInt(GlobalSettingKeys.PremiumMonthlyCredits, 5000),
            DailySoftCap        = _settings.GetInt(GlobalSettingKeys.PremiumDailyCap, 250),
        };
    }
}

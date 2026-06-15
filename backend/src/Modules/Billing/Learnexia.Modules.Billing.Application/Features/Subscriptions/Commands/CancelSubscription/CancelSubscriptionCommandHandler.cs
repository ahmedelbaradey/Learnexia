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
using System.Linq;

namespace Learnexia.Modules.Billing.Application.Features.Subscriptions.Commands.CancelSubscription;

/// <summary>
/// Cancels the parent's subscription.
///
/// <para>State transitions:
/// <list type="bullet">
///   <item><c>Active Premium</c> → <c>Canceled</c> (access until <c>CurrentCycleEnd</c>).</item>
///   <item><c>PendingPayment</c> → <c>Canceled</c> immediately (no payment was taken).</item>
///   <item><c>Downgrading</c> → <c>Canceled</c> immediately.</item>
///   <item><c>Active Free</c> → <c>Canceled</c> (no-op in terms of access; row marked).</item>
/// </list>
/// </para>
///
/// <para>IDOR guard: <c>ParentUserId</c> comes from JWT (controller).</para>
/// </summary>
public class CancelSubscriptionCommandHandler
    : BaseResponseHandler, ICommandHandler<CancelSubscriptionCommand, BaseResponse<SubscriptionDto>>
{
    private readonly IBillingDbContext _db;
    private readonly IGlobalSettingsProvider _settings;
    private readonly IPaymentProvider _paymentProvider;
    private readonly ILoggerManager _logger;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public CancelSubscriptionCommandHandler(
        IBillingDbContext db,
        IGlobalSettingsProvider settings,
        IPaymentProvider paymentProvider,
        ILoggerManager logger,
        ICurrentUserService currentUser,
        IStringLocalizer<SharedResources> localizer)
    {
        _db = db;
        _settings = settings;
        _paymentProvider = paymentProvider;
        _logger = logger;
        _currentUser = currentUser;
        _localizer = localizer;
    }

    public async Task<BaseResponse<SubscriptionDto>> Handle(
        CancelSubscriptionCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

            // Find any non-canceled subscription for this parent.
            var subscription = await _db.Subscriptions
                .Where(s => s.ParentUserId == request.ParentUserId
                         && s.Status != SubscriptionStatus.Canceled)
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

            // Look up the most recent succeeded payment's provider ref for cancel-recurring.
            var providerRef = await _db.Payments
                .Where(p => p.SubscriptionId == subscription.Id
                         && p.Status == Domain.Enums.PaymentStatus.Succeeded
                         && p.ProviderPaymentRef != null)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => p.ProviderPaymentRef)
                .FirstOrDefaultAsync(cancellationToken);

            subscription.Status          = SubscriptionStatus.Canceled;
            subscription.PendingPlanCode = null;
            subscription.PendingBillingPeriod = null;

            await _db.SaveChangesAsync(_currentUser.UserId ?? 0);
            await tx.CommitAsync(cancellationToken);

            // Attempt provider-side cancel-recurring AFTER the local commit (Fake = no-op; live = API call).
            // A provider failure must NOT roll back the local Canceled state.
            if (!string.IsNullOrEmpty(providerRef))
            {
                try
                {
                    await _paymentProvider.CancelRecurringAsync(providerRef, cancellationToken);
                }
                catch (Exception cancelEx)
                {
                    // Log and continue — local state is already Canceled; dunning handles provider side.
                    _logger.LogError(cancelEx, $"CancelSubscription: provider cancel-recurring failed for parentId={request.ParentUserId}. Local state is Canceled.");
                }
            }

            _logger.LogInfo($"CancelSubscription: parentId={request.ParentUserId} → Canceled.");

            return new BaseResponse<SubscriptionDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.SubscriptionCanceledSuccessfully],
                Data       = BuildDto(subscription),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in CancelSubscriptionCommand for parentUserId={request.ParentUserId}");
            return ServerError<SubscriptionDto>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }

    private SubscriptionDto BuildDto(Domain.Entities.Subscription s)
    {
        var (monthly, daily) = s.PlanCode == PlanCode.Premium
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

using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Subscriptions.Dtos;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="ISubscriptionService"/> (Option C).
///
/// <para>Owns ALL EF Core access, transaction management, and subscription state-machine
/// logic for cancel, downgrade, upgrade, and get-current-plan. Application-layer handlers
/// inject this interface and stay EF-free.</para>
///
/// <para><strong>IDOR guard:</strong> <c>parentUserId</c> MUST come from JWT — the handler
/// resolves it via <c>ICurrentUserService.UserId</c> before calling these methods.</para>
/// </summary>
public sealed class SubscriptionService : ISubscriptionService
{
    private readonly BillingDbContext _db;
    private readonly IGlobalSettingsProvider _settings;
    private readonly IPaymentProvider _paymentProvider;
    private readonly ILoggerManager _logger;

    public SubscriptionService(
        BillingDbContext db,
        IGlobalSettingsProvider settings,
        IPaymentProvider paymentProvider,
        ILoggerManager logger)
    {
        _db              = db;
        _settings        = settings;
        _paymentProvider = paymentProvider;
        _logger          = logger;
    }

    // ── CancelAsync ──────────────────────────────────────────────────────────────

    public async Task<SubscriptionOperationResult> CancelAsync(
        int parentUserId,
        int actorUserId,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var subscription = await _db.Subscriptions
            .Where(s => s.ParentUserId == parentUserId && s.Status != SubscriptionStatus.Canceled)
            .FirstOrDefaultAsync(ct);

        if (subscription is null)
        {
            await tx.RollbackAsync(ct);
            return SubscriptionOperationResult.Fail(SubscriptionOperationFailureReason.SubscriptionNotFound);
        }

        // Look up the most recent succeeded payment's provider ref for cancel-recurring.
        var providerRef = await _db.Payments
            .Where(p => p.SubscriptionId == subscription.Id
                     && p.Status == PaymentStatus.Succeeded
                     && p.ProviderPaymentRef != null)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => p.ProviderPaymentRef)
            .FirstOrDefaultAsync(ct);

        subscription.Status               = SubscriptionStatus.Canceled;
        subscription.PendingPlanCode      = null;
        subscription.PendingBillingPeriod = null;

        await _db.SaveChangesAsync(actorUserId);
        await tx.CommitAsync(ct);

        // Attempt provider-side cancel-recurring AFTER local commit.
        // A provider failure must NOT roll back the local Canceled state.
        if (!string.IsNullOrEmpty(providerRef))
        {
            try
            {
                await _paymentProvider.CancelRecurringAsync(providerRef, ct);
            }
            catch (Exception cancelEx)
            {
                _logger.LogError(cancelEx,
                    $"SubscriptionService.Cancel: provider cancel-recurring failed for parentId={parentUserId}. Local state is Canceled.");
            }
        }

        _logger.LogInfo($"SubscriptionService.Cancel: parentId={parentUserId} → Canceled.");

        return SubscriptionOperationResult.Ok(BuildDto(subscription));
    }

    // ── RequestDowngradeAsync ─────────────────────────────────────────────────────

    public async Task<SubscriptionOperationResult> RequestDowngradeAsync(
        int parentUserId,
        int actorUserId,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var subscription = await _db.Subscriptions
            .Where(s => s.ParentUserId == parentUserId && s.Status == SubscriptionStatus.Active)
            .FirstOrDefaultAsync(ct);

        if (subscription is null)
        {
            await tx.RollbackAsync(ct);
            return SubscriptionOperationResult.Fail(SubscriptionOperationFailureReason.SubscriptionNotFound);
        }

        if (subscription.PlanCode == PlanCode.Free)
        {
            await tx.RollbackAsync(ct);
            return SubscriptionOperationResult.Fail(SubscriptionOperationFailureReason.AlreadyFree);
        }

        subscription.Status          = SubscriptionStatus.Downgrading;
        subscription.PendingPlanCode = PlanCode.Free;

        await _db.SaveChangesAsync(actorUserId);
        await tx.CommitAsync(ct);

        _logger.LogInfo($"SubscriptionService.RequestDowngrade: parentId={parentUserId} → Downgrading.");

        return SubscriptionOperationResult.Ok(BuildDto(subscription));
    }

    // ── RequestUpgradeAsync ───────────────────────────────────────────────────────

    public async Task<SubscriptionOperationResult> RequestUpgradeAsync(
        int parentUserId,
        BillingPeriod billingPeriod,
        int actorUserId,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var subscription = await _db.Subscriptions
            .Where(s => s.ParentUserId == parentUserId && s.Status == SubscriptionStatus.Active)
            .FirstOrDefaultAsync(ct);

        if (subscription is null)
        {
            // No active subscription row — create one in PendingPayment.
            subscription = new Subscription
            {
                ParentUserId  = parentUserId,
                PlanCode      = PlanCode.Free,
                BillingPeriod = billingPeriod,
                Status        = SubscriptionStatus.PendingPayment,
            };
            await _db.Subscriptions.AddAsync(subscription, ct);
        }
        else if (subscription.PlanCode == PlanCode.Premium
                 && subscription.Status == SubscriptionStatus.Active)
        {
            // Already Premium Active — record the cadence preference as a pending period change.
            subscription.PendingBillingPeriod = billingPeriod;
        }
        else
        {
            // Free/Downgrading → upgrade intent.
            subscription.BillingPeriod = billingPeriod;
            subscription.Status        = SubscriptionStatus.PendingPayment;
        }

        await _db.SaveChangesAsync(actorUserId);
        await tx.CommitAsync(ct);

        _logger.LogInfo(
            $"SubscriptionService.RequestUpgrade: parentId={parentUserId} → PendingPayment, period={billingPeriod}.");

        return SubscriptionOperationResult.Ok(BuildDto(subscription));
    }

    // ── GetCurrentPlanAsync ───────────────────────────────────────────────────────

    public async Task<SubscriptionDto?> GetCurrentPlanAsync(int parentUserId, CancellationToken ct)
    {
        var subscription = await _db.Subscriptions
            .AsNoTracking()
            .Where(s => s.ParentUserId == parentUserId && s.Status == SubscriptionStatus.Active)
            .FirstOrDefaultAsync(ct);

        // Parents without a subscription row are on Free (row may not exist yet).
        var planCode      = subscription?.PlanCode ?? PlanCode.Free;
        var billingPeriod = subscription?.BillingPeriod ?? BillingPeriod.Monthly;
        var status        = subscription?.Status ?? SubscriptionStatus.Active;

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
            .FirstOrDefaultAsync(ct)
            ?? planCode.ToString();

        return new SubscriptionDto
        {
            Id                   = subscription?.Id ?? 0,
            PlanCode             = planCode,
            PlanName             = planName,
            BillingPeriod        = billingPeriod,
            Status               = status,
            CurrentCycleStart    = subscription?.CurrentCycleStart,
            CurrentCycleEnd      = subscription?.CurrentCycleEnd,
            PendingPlanCode      = subscription?.PendingPlanCode,
            PendingBillingPeriod = subscription?.PendingBillingPeriod,
            MonthlyCredits       = monthlyCredits,
            DailySoftCap         = dailyCap,
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private SubscriptionDto BuildDto(Subscription s)
    {
        var planCode = s.PlanCode;
        var (monthly, daily) = planCode == PlanCode.Premium
            ? (_settings.GetInt(GlobalSettingKeys.PremiumMonthlyCredits, 5000),
               _settings.GetInt(GlobalSettingKeys.PremiumDailyCap, 250))
            : (_settings.GetInt(GlobalSettingKeys.FreeMonthlyCredits, 100),
               _settings.GetInt(GlobalSettingKeys.FreeDailyCap, 10));

        return new SubscriptionDto
        {
            Id                   = s.Id,
            PlanCode             = s.PlanCode,
            PlanName             = s.PlanCode.ToString(),
            BillingPeriod        = s.BillingPeriod,
            Status               = s.Status,
            CurrentCycleStart    = s.CurrentCycleStart,
            CurrentCycleEnd      = s.CurrentCycleEnd,
            PendingPlanCode      = s.PendingPlanCode,
            PendingBillingPeriod = s.PendingBillingPeriod,
            MonthlyCredits       = monthly,
            DailySoftCap         = daily,
        };
    }
}

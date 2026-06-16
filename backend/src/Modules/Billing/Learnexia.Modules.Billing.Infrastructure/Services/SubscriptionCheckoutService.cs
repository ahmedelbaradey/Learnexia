using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Payments.Dtos;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="ISubscriptionCheckoutService"/> (Option C).
///
/// <para>Owns ALL EF Core access, provider calls, and amount resolution for subscription
/// checkout initiation. Application-layer handlers inject this interface and stay EF-free.</para>
///
/// <para><strong>Security:</strong> amount is always resolved server-side from
/// <c>IGlobalSettingsProvider</c> — never client-supplied. <c>parentUserId</c> comes from JWT.</para>
/// </summary>
public sealed class SubscriptionCheckoutService : ISubscriptionCheckoutService
{
    private readonly BillingDbContext _db;
    private readonly IPaymentProvider _paymentProvider;
    private readonly IGlobalSettingsProvider _settings;
    private readonly ILoggerManager _logger;

    public SubscriptionCheckoutService(
        BillingDbContext db,
        IPaymentProvider paymentProvider,
        IGlobalSettingsProvider settings,
        ILoggerManager logger)
    {
        _db              = db;
        _paymentProvider = paymentProvider;
        _settings        = settings;
        _logger          = logger;
    }

    public async Task<SubscriptionCheckoutResult> StartAsync(
        int parentUserId,
        int actorUserId,
        CancellationToken ct)
    {
        // 1. Load the PendingPayment subscription.
        var subscription = await _db.Subscriptions
            .Where(s => s.ParentUserId == parentUserId
                     && s.Status == SubscriptionStatus.PendingPayment)
            .FirstOrDefaultAsync(ct);

        if (subscription is null)
            return SubscriptionCheckoutResult.Fail(SubscriptionCheckoutFailureReason.SubscriptionNotFound);

        // 2. Resolve amount SERVER-SIDE — client never supplies the amount.
        var amount = subscription.BillingPeriod == BillingPeriod.Annual
            ? _settings.GetDecimal(GlobalSettingKeys.SubscriptionAnnualPriceEgp, 1990m)
            : _settings.GetDecimal(GlobalSettingKeys.SubscriptionMonthlyPriceEgp, 199m);

        // 3. Create the Payment row (Initiated) with a unique idempotency key.
        var idempotencyKey = $"sub-checkout:{parentUserId}:{subscription.Id}:{Guid.NewGuid():N}";

        var payment = new Payment
        {
            SubscriptionId = subscription.Id,
            ParentUserId   = parentUserId,
            Amount         = amount,
            Currency       = PaymentCurrency.EGP,
            Status         = PaymentStatus.Initiated,
            Kind           = PaymentKind.Subscription,
            IdempotencyKey = idempotencyKey,
        };

        await _db.Payments.AddAsync(payment, ct);
        await _db.SaveChangesAsync(actorUserId);

        // 4. Call the provider (outside write transaction — can be slow).
        CheckoutSession session;
        try
        {
            session = await _paymentProvider.CreateCheckoutSessionAsync(payment, ct);
        }
        catch (Exception provEx)
        {
            _logger.LogError(provEx,
                $"SubscriptionCheckoutService: provider call failed for parentId={parentUserId}");
            // Payment stays Initiated — reconcile job sweeps it later.
            return SubscriptionCheckoutResult.Fail(SubscriptionCheckoutFailureReason.ProviderUnavailable);
        }

        // 5. Persist the provider ref.
        payment.ProviderPaymentRef = session.ProviderPaymentRef;
        await _db.SaveChangesAsync(actorUserId);

        _logger.LogInfo(
            $"SubscriptionCheckoutService: paymentId={payment.Id}, parentId={parentUserId}, " +
            $"period={subscription.BillingPeriod}, amount={amount}.");

        return SubscriptionCheckoutResult.Ok(new CheckoutResultDto
        {
            PaymentId          = payment.Id,
            RedirectUrl        = session.RedirectUrl,
            ProviderPaymentRef = session.ProviderPaymentRef,
        });
    }
}

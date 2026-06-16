using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IWebhookEventService"/> (Option C).
///
/// <para>Owns ALL EF Core access, transaction management, idempotency, 23505 handling, and
/// post-commit integration-event staging for webhook processing. Application-layer handlers
/// inject <see cref="IWebhookEventService"/> and never reference EF or <c>BillingDbContext</c>.</para>
///
/// <para><strong>Security:</strong> signature verification is performed by the CALLER (handler)
/// before dispatching to this service. This service assumes the payload is already verified.</para>
///
/// <para><strong>Idempotency:</strong> the <c>WebhookEvent.ProviderEventId</c> unique index
/// is the outer guard. A 23505 violation from a concurrent race is caught and treated as an
/// idempotent success.</para>
///
/// <para><strong>Post-commit events:</strong> integration events fire AFTER <c>CommitAsync</c>
/// so consumers see consistent state. A publish failure does NOT roll back committed data.</para>
/// </summary>
public sealed class WebhookEventService : IWebhookEventService
{
    private readonly BillingDbContext _db;
    private readonly IEnergyPackService _energyPackService;
    private readonly IRefundService _refundService;
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public WebhookEventService(
        BillingDbContext db,
        IEnergyPackService energyPackService,
        IRefundService refundService,
        IPublisher publisher,
        ILoggerManager logger)
    {
        _db                = db;
        _energyPackService = energyPackService;
        _refundService     = refundService;
        _publisher         = publisher;
        _logger            = logger;
    }

    // ── IsAlreadyProcessedAsync ───────────────────────────────────────────────────

    public async Task<bool> IsAlreadyProcessedAsync(string providerEventId, CancellationToken ct)
        => await _db.WebhookEvents.AnyAsync(w => w.ProviderEventId == providerEventId, ct);

    // ── HandlePaymentSucceededAsync ───────────────────────────────────────────────

    public async Task<WebhookProcessResult> HandlePaymentSucceededAsync(
        ParsedWebhookEvent parsed,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var webhookRecord = new WebhookEvent
            {
                ProviderEventId = parsed.ProviderEventId,
                EventType       = parsed.EventType,
                Payload         = parsed.RawPayload,
                Succeeded       = false,
            };
            await _db.WebhookEvents.AddAsync(webhookRecord, ct);

            var payment = await _db.Payments
                .FirstOrDefaultAsync(p => p.ProviderPaymentRef == parsed.PaymentRef, ct);

            if (payment is null)
            {
                webhookRecord.ProcessedAt = DateTime.UtcNow;
                webhookRecord.Succeeded   = false;
                await _db.SaveChangesAsync(0);
                await tx.CommitAsync(ct);

                _logger.LogInfo(
                    $"WebhookEventService: payment.succeeded for unknown ref={parsed.PaymentRef} — recorded.");
                return WebhookProcessResult.Ok("PaymentNotFound");
            }

            // Amount reconciliation: server-side amount is authoritative.
            if (Math.Abs(parsed.AmountFromProvider - payment.Amount) > 0.01m)
            {
                _logger.LogInfo(
                    $"WebhookEventService: amount mismatch — provider={parsed.AmountFromProvider}, server={payment.Amount}. Using server-side.");
            }

            // ── Pack path — delegate to IEnergyPackService (Option C seam) ──────────
            if (payment.Kind == PaymentKind.Pack)
            {
                webhookRecord.ProcessedAt = DateTime.UtcNow;
                webhookRecord.Succeeded   = true;
                await _db.SaveChangesAsync(0);
                await tx.CommitAsync(ct);

                if (!payment.TargetChildId.HasValue)
                {
                    _logger.LogInfo(
                        $"WebhookEventService: Pack payment {payment.Id} has no TargetChildId — skipping credit.");
                    return WebhookProcessResult.Ok("PackNoTarget");
                }

                var creditResult = await _energyPackService.CreditPurchasedPackAsync(
                    paymentId      : payment.Id,
                    targetChildId  : payment.TargetChildId.Value,
                    providerEventId: parsed.ProviderEventId,
                    ct             : ct);

                _logger.LogInfo(
                    $"WebhookEventService: Pack credited — paymentId={payment.Id}, childId={payment.TargetChildId.Value}, duplicate={creditResult.WasDuplicate}.");

                return WebhookProcessResult.Ok(creditResult.WasDuplicate ? "PackDuplicate" : "PackCredited");
            }

            // ── Subscription path ─────────────────────────────────────────────────────
            payment.Status             = PaymentStatus.Succeeded;
            payment.ProviderPaymentRef = parsed.PaymentRef;

            Subscription? subscription = null;
            if (payment.SubscriptionId.HasValue)
            {
                subscription = await _db.Subscriptions
                    .FirstOrDefaultAsync(s => s.Id == payment.SubscriptionId.Value, ct);

                if (subscription is not null)
                {
                    var nowUtc = DateTime.UtcNow;
                    subscription.PlanCode    = PlanCode.Premium;
                    subscription.Status      = SubscriptionStatus.Active;

                    if (subscription.PendingBillingPeriod.HasValue)
                        subscription.BillingPeriod = subscription.PendingBillingPeriod.Value;

                    subscription.CurrentCycleStart   = nowUtc;
                    subscription.CurrentCycleEnd     = subscription.BillingPeriod == BillingPeriod.Annual
                        ? nowUtc.AddYears(1)
                        : nowUtc.AddMonths(1);

                    subscription.PendingPlanCode      = null;
                    subscription.PendingBillingPeriod = null;
                }
            }

            webhookRecord.ProcessedAt = DateTime.UtcNow;
            webhookRecord.Succeeded   = true;

            await _db.SaveChangesAsync(0);
            await tx.CommitAsync(ct);

            _logger.LogInfo(
                $"WebhookEventService: payment.succeeded — paymentId={payment.Id}, parentId={payment.ParentUserId}.");

            // POST-COMMIT: fire AFTER commit so consumers see consistent state.
            if (subscription is not null)
            {
                await _publisher.Publish(new SubscriptionActivatedIntegrationEvent(
                    EventId       : Guid.NewGuid(),
                    OccurredOnUtc : DateTime.UtcNow,
                    ParentUserId  : payment.ParentUserId,
                    SubscriptionId: subscription.Id,
                    PaymentId     : payment.Id,
                    BillingPeriod : subscription.BillingPeriod.ToString()), ct);
            }

            return WebhookProcessResult.Ok("PaymentSucceeded");
        }
        catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
        {
            await tx.RollbackAsync(ct);
            _logger.LogInfo(
                $"WebhookEventService: concurrent duplicate for eventId={parsed.ProviderEventId} — no-op.");
            return WebhookProcessResult.ConcurrentDuplicate();
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── HandlePaymentFailedAsync ──────────────────────────────────────────────────

    public async Task<WebhookProcessResult> HandlePaymentFailedAsync(
        ParsedWebhookEvent parsed,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var webhookRecord = new WebhookEvent
            {
                ProviderEventId = parsed.ProviderEventId,
                EventType       = parsed.EventType,
                Payload         = parsed.RawPayload,
                Succeeded       = false,
            };
            await _db.WebhookEvents.AddAsync(webhookRecord, ct);

            var payment = await _db.Payments
                .FirstOrDefaultAsync(p => p.ProviderPaymentRef == parsed.PaymentRef, ct);

            if (payment is not null)
                payment.Status = PaymentStatus.Failed;

            webhookRecord.ProcessedAt = DateTime.UtcNow;
            webhookRecord.Succeeded   = true;

            await _db.SaveChangesAsync(0);
            await tx.CommitAsync(ct);

            _logger.LogInfo(
                $"WebhookEventService: payment.failed — paymentRef={parsed.PaymentRef}.");

            // POST-COMMIT dunning notification.
            await _publisher.Publish(new PaymentFailedIntegrationEvent(
                EventId       : Guid.NewGuid(),
                OccurredOnUtc : DateTime.UtcNow,
                ParentUserId  : payment?.ParentUserId ?? 0,
                PaymentId     : payment?.Id ?? 0,
                SubscriptionId: payment?.SubscriptionId), ct);

            return WebhookProcessResult.Ok("PaymentFailed");
        }
        catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
        {
            await tx.RollbackAsync(ct);
            return WebhookProcessResult.ConcurrentDuplicate();
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── HandleChargeFailedAsync ───────────────────────────────────────────────────

    public async Task<WebhookProcessResult> HandleChargeFailedAsync(
        ParsedWebhookEvent parsed,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var webhookRecord = new WebhookEvent
            {
                ProviderEventId = parsed.ProviderEventId,
                EventType       = parsed.EventType,
                Payload         = parsed.RawPayload,
                Succeeded       = false,
            };
            await _db.WebhookEvents.AddAsync(webhookRecord, ct);

            var payment = await _db.Payments
                .FirstOrDefaultAsync(p => p.ProviderPaymentRef == parsed.PaymentRef, ct);

            webhookRecord.ProcessedAt = DateTime.UtcNow;
            webhookRecord.Succeeded   = true;
            await _db.SaveChangesAsync(0);
            await tx.CommitAsync(ct);

            if (payment is null || !payment.SubscriptionId.HasValue)
            {
                _logger.LogInfo(
                    $"WebhookEventService: charge.failed for unknown ref={parsed.PaymentRef} — recorded.");
                return WebhookProcessResult.Ok("ChargeFailedNoSubscription");
            }

            // Delegate dunning to IRefundService (Option C — EF-free from here).
            var result = await _refundService.ProcessChargeFailedAsync(
                paymentId      : payment.Id,
                subscriptionId : payment.SubscriptionId.Value,
                providerEventId: parsed.ProviderEventId,
                ct             : ct);

            _logger.LogInfo(
                $"WebhookEventService: charge.failed processed — subscriptionId={payment.SubscriptionId.Value}, " +
                $"attempts={result.FailedAttemptCount}, downgradeScheduled={result.DowngradeScheduled}.");

            // POST-COMMIT dunning notification.
            await _publisher.Publish(new PaymentFailedIntegrationEvent(
                EventId       : Guid.NewGuid(),
                OccurredOnUtc : DateTime.UtcNow,
                ParentUserId  : payment.ParentUserId,
                PaymentId     : payment.Id,
                SubscriptionId: payment.SubscriptionId), ct);

            return WebhookProcessResult.Ok(
                result.DowngradeScheduled ? "DunningDowngradeScheduled" : "DunningRetryScheduled");
        }
        catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
        {
            await tx.RollbackAsync(ct);
            return WebhookProcessResult.ConcurrentDuplicate();
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── HandleRefundSucceededAsync ────────────────────────────────────────────────

    public async Task<WebhookProcessResult> HandleRefundSucceededAsync(
        ParsedWebhookEvent parsed,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var webhookRecord = new WebhookEvent
            {
                ProviderEventId = parsed.ProviderEventId,
                EventType       = parsed.EventType,
                Payload         = parsed.RawPayload,
                Succeeded       = false,
            };
            await _db.WebhookEvents.AddAsync(webhookRecord, ct);

            var payment = await _db.Payments
                .FirstOrDefaultAsync(p => p.ProviderPaymentRef == parsed.PaymentRef, ct);

            webhookRecord.ProcessedAt = DateTime.UtcNow;
            webhookRecord.Succeeded   = true;
            await _db.SaveChangesAsync(0);
            await tx.CommitAsync(ct);

            if (payment is null)
            {
                _logger.LogInfo(
                    $"WebhookEventService: refund.succeeded for unknown ref={parsed.PaymentRef} — recorded.");
                return WebhookProcessResult.Ok("RefundPaymentNotFound");
            }

            // Delegate refund clawback to IRefundService (Option C — EF-free from here).
            string outcome;
            if (payment.Kind == PaymentKind.Pack)
            {
                var result = await _refundService.ProcessPackRefundAsync(
                    paymentId      : payment.Id,
                    providerEventId: parsed.ProviderEventId,
                    ct             : ct);

                _logger.LogInfo(
                    $"WebhookEventService: pack refund — paymentId={payment.Id}, " +
                    $"clawedBack={result.ClawedBackAmount}, duplicate={result.WasDuplicate}.");

                outcome = result.WasDuplicate ? "PackRefundDuplicate" : $"PackRefunded:{result.ClawedBackAmount}";
            }
            else
            {
                var result = await _refundService.ProcessSubscriptionRefundAsync(
                    paymentId      : payment.Id,
                    providerEventId: parsed.ProviderEventId,
                    ct             : ct);

                _logger.LogInfo(
                    $"WebhookEventService: subscription refund — paymentId={payment.Id}, " +
                    $"duplicate={result.WasDuplicate}.");

                outcome = result.WasDuplicate ? "SubscriptionRefundDuplicate" : "SubscriptionRefunded";
            }

            return WebhookProcessResult.Ok(outcome);
        }
        catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
        {
            await tx.RollbackAsync(ct);
            return WebhookProcessResult.ConcurrentDuplicate();
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── HandleUnknownEventAsync ───────────────────────────────────────────────────

    public async Task<WebhookProcessResult> HandleUnknownEventAsync(
        ParsedWebhookEvent parsed,
        CancellationToken ct)
    {
        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            var webhookRecord = new WebhookEvent
            {
                ProviderEventId = parsed.ProviderEventId,
                EventType       = parsed.EventType,
                Payload         = parsed.RawPayload,
                ProcessedAt     = DateTime.UtcNow,
                Succeeded       = true,
            };
            await _db.WebhookEvents.AddAsync(webhookRecord, ct);
            await _db.SaveChangesAsync(0);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
        {
            // Duplicate — ignore.
        }

        _logger.LogInfo(
            $"WebhookEventService: unhandled eventType={parsed.EventType}, id={parsed.ProviderEventId}.");
        return WebhookProcessResult.Ok("Unhandled");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("23505", StringComparison.Ordinal) == true
           || ex.InnerException?.Message.Contains("UX_WebhookEvents_ProviderEventId",
               StringComparison.OrdinalIgnoreCase) == true;
}

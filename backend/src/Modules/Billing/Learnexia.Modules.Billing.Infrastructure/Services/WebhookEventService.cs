using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
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
    private readonly ISeatService _seatService;
    private readonly IRefundService _refundService;
    private readonly IGlobalSettingsProvider _settings;
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public WebhookEventService(
        BillingDbContext db,
        IEnergyPackService energyPackService,
        ISeatService seatService,
        IRefundService refundService,
        IGlobalSettingsProvider settings,
        IPublisher publisher,
        ILoggerManager logger)
    {
        _db                = db;
        _energyPackService = energyPackService;
        _seatService       = seatService;
        _refundService     = refundService;
        _settings          = settings;
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

            // ── Seat path (P10-14-BE-7) — flip Payment → Succeeded + increment purchased seats ──
            // BLOCKER-3 FIX: the Payment flip + seat increment + WebhookEvent.Succeeded must all
            // commit in ONE transaction. Committing the event before the seat increment (the old
            // two-transaction pattern) was a money-atomicity hole: a crash between txn-1 and txn-2
            // blocked replay (event already recorded as processed) but never minted the seat.
            if (payment.Kind == PaymentKind.Seat)
            {
                // SECURITY #1 (per-payment idempotency): only the FIRST successful callback for a
                // given seat payment may increment PurchasedExtraSeats. The outer
                // WebhookEvent.ProviderEventId unique guard stops same-id replay, but a provider that
                // emits TWO payment.succeeded events with DISTINCT event ids for the SAME payment would
                // otherwise re-enter this branch and double-grant the seat (parent paid once). Gate on
                // the payment not already being terminal — the flip to Succeeded is the single-shot lock.
                if (payment.Status != PaymentStatus.Initiated)
                {
                    webhookRecord.ProcessedAt = DateTime.UtcNow;
                    webhookRecord.Succeeded   = true;
                    await _db.SaveChangesAsync(0);
                    await tx.CommitAsync(ct);

                    _logger.LogInfo(
                        $"WebhookEventService: Seat payment {payment.Id} already in status {payment.Status} — " +
                        $"duplicate succeeded callback ignored; PurchasedExtraSeats NOT incremented again.");
                    return WebhookProcessResult.Ok("SeatPaymentDuplicate");
                }

                payment.Status = PaymentStatus.Succeeded;

                if (!payment.SubscriptionId.HasValue)
                {
                    // No subscription → record event as processed (nothing else to do) and return.
                    webhookRecord.ProcessedAt = DateTime.UtcNow;
                    webhookRecord.Succeeded   = true;
                    await _db.SaveChangesAsync(0);
                    await tx.CommitAsync(ct);

                    _logger.LogInfo(
                        $"WebhookEventService: Seat payment {payment.Id} has no SubscriptionId — recorded but seats NOT incremented.");
                    return WebhookProcessResult.Ok("SeatPaymentNoSubscription");
                }

                // Resolve the subscription and increment seats INSIDE the current transaction.
                // NIT (b): predicate includes parentUserId for defence-in-depth IDOR guard.
                var sub = await _db.Subscriptions
                    .FirstOrDefaultAsync(s => s.Id == payment.SubscriptionId.Value
                                           && s.ParentUserId == payment.ParentUserId, ct);

                string seatOutcome;
                if (sub is null)
                {
                    seatOutcome = "SeatSubscriptionNotFound";
                    _logger.LogInfo(
                        $"WebhookEventService: Seat payment {payment.Id} — subscription {payment.SubscriptionId.Value} not found for parent={payment.ParentUserId}.");
                }
                else
                {
                    // WEBHOOK-SEAT-04: enforce the seats.max ceiling. Checkout (StartSeatCheckoutAsync)
                    // already guards this, but the webhook must defend it too — a provider replay or a
                    // direct callback could otherwise push total seats above the cap. The guard is
                    // INLINE here (the single source of truth) because it must run inside the blocker-3
                    // single transaction — a separate service method would open its own tx and couldn't join.
                    var maxSeats = _settings.GetInt(GlobalSettingKeys.SeatsMax, 5);
                    var plan = await _db.Plans.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Code == sub.PlanCode, ct);
                    var includedSeats = plan?.IncludedSeats
                        ?? _settings.GetInt(GlobalSettingKeys.SeatsIncludedFree, 1);

                    if (includedSeats + sub.PurchasedExtraSeats + 1 > maxSeats)
                    {
                        seatOutcome = "SeatExceedsMaxSeats";
                        _logger.LogInfo(
                            $"WebhookEventService: Seat payment {payment.Id} — would exceed seats.max={maxSeats} " +
                            $"(included={includedSeats}, purchasedExtra={sub.PurchasedExtraSeats}); seat NOT incremented.");
                    }
                    else
                    {
                        sub.PurchasedExtraSeats += 1;
                        seatOutcome = "SeatPaymentConfirmed";

                        // BE-11: append SeatLedgerEntry{Purchased} for the new seat.
                        var ledgerEntry = new SeatLedgerEntry
                        {
                            SubscriptionId = sub.Id,
                            ParentUserId   = payment.ParentUserId,
                            EventType      = SeatLedgerEventType.Purchased,
                            Quantity       = 1,
                            Amount         = payment.Amount,
                            IdempotencyKey = $"seat-purchase:{payment.Id}:{parsed.ProviderEventId}",
                            OccurredAt     = DateTime.UtcNow,
                            CreatedAt      = DateTime.UtcNow,
                            CreatedBy      = 0,
                        };
                        await _db.SeatLedgerEntries.AddAsync(ledgerEntry, ct);

                        _logger.LogInfo(
                            $"WebhookEventService: Seat payment {payment.Id} — incremented PurchasedExtraSeats for " +
                            $"subscriptionId={sub.Id}, parentId={payment.ParentUserId}.");
                    }
                }

                // Mark the webhook event as succeeded and commit everything atomically.
                webhookRecord.ProcessedAt = DateTime.UtcNow;
                webhookRecord.Succeeded   = true;
                await _db.SaveChangesAsync(0);
                await tx.CommitAsync(ct);

                return WebhookProcessResult.Ok(seatOutcome);
            }

            // ── SeatReactivation path (P10-15-BE-7) ──────────────────────────────────────
            // flip NoSeatLocked → Active, re-join allocation row, append SeatLedgerEntry{Reactivated}.
            if (payment.Kind == PaymentKind.SeatReactivation)
            {
                // Idempotency: payment must still be Initiated (single-shot lock like Seat branch).
                if (payment.Status != PaymentStatus.Initiated)
                {
                    webhookRecord.ProcessedAt = DateTime.UtcNow;
                    webhookRecord.Succeeded   = true;
                    await _db.SaveChangesAsync(0);
                    await tx.CommitAsync(ct);

                    _logger.LogInfo(
                        $"WebhookEventService: SeatReactivation payment {payment.Id} already in status {payment.Status} — duplicate ignored.");
                    return WebhookProcessResult.Ok("SeatReactivationDuplicate");
                }

                payment.Status = PaymentStatus.Succeeded;

                if (!payment.SubscriptionId.HasValue || !payment.TargetChildId.HasValue)
                {
                    webhookRecord.ProcessedAt = DateTime.UtcNow;
                    webhookRecord.Succeeded   = true;
                    await _db.SaveChangesAsync(0);
                    await tx.CommitAsync(ct);

                    _logger.LogInfo(
                        $"WebhookEventService: SeatReactivation payment {payment.Id} missing SubscriptionId/TargetChildId — recorded.");
                    return WebhookProcessResult.Ok("SeatReactivationMissingContext");
                }

                var reactivationSub = await _db.Subscriptions
                    .FirstOrDefaultAsync(s => s.Id == payment.SubscriptionId.Value
                                           && s.ParentUserId == payment.ParentUserId, ct);

                if (reactivationSub is null)
                {
                    webhookRecord.ProcessedAt = DateTime.UtcNow;
                    webhookRecord.Succeeded   = true;
                    await _db.SaveChangesAsync(0);
                    await tx.CommitAsync(ct);

                    _logger.LogInfo(
                        $"WebhookEventService: SeatReactivation payment {payment.Id} — subscription not found.");
                    return WebhookProcessResult.Ok("SeatReactivationNoSubscription");
                }

                // Flip the child's seat from NoSeatLocked to Active.
                var lockedReservation = await _db.SeatReservations
                    .FirstOrDefaultAsync(r => r.SubscriptionId == payment.SubscriptionId.Value
                                           && r.ChildId        == payment.TargetChildId.Value
                                           && (r.Status == SeatStatus.Active || r.Status == SeatStatus.Reserved)
                                           && r.SeatState == SeatState.NoSeatLocked, ct);

                string reactivationOutcome;
                if (lockedReservation is null)
                {
                    reactivationOutcome = "SeatReactivationNotLocked";
                    _logger.LogInfo(
                        $"WebhookEventService: SeatReactivation payment {payment.Id} — child {payment.TargetChildId.Value} not in NoSeatLocked state; no-op.");
                }
                else
                {
                    lockedReservation.SeatState = SeatState.Active;

                    // Append SeatLedgerEntry{Reactivated}.
                    var reactivationLedger = new SeatLedgerEntry
                    {
                        SubscriptionId = payment.SubscriptionId.Value,
                        ParentUserId   = payment.ParentUserId,
                        ChildId        = payment.TargetChildId.Value,
                        EventType      = SeatLedgerEventType.Reactivated,
                        Amount         = payment.Amount,
                        IdempotencyKey = $"seat-reactivation:{payment.Id}:{parsed.ProviderEventId}",
                        OccurredAt     = DateTime.UtcNow,
                        CreatedAt      = DateTime.UtcNow,
                        CreatedBy      = 0,
                    };
                    await _db.SeatLedgerEntries.AddAsync(reactivationLedger, ct);
                    reactivationOutcome = "SeatReactivationConfirmed";

                    _logger.LogInfo(
                        $"WebhookEventService: SeatReactivation — child {payment.TargetChildId.Value} reactivated " +
                        $"under subscriptionId={payment.SubscriptionId.Value}.");
                }

                webhookRecord.ProcessedAt = DateTime.UtcNow;
                webhookRecord.Succeeded   = true;
                await _db.SaveChangesAsync(0);
                await tx.CommitAsync(ct);

                return WebhookProcessResult.Ok(reactivationOutcome);
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

                    // FINDING-15-B: a successful payment resolves any open payment-failure grace
                    // window. Grace is ONLY ever payment-failure (SeatGraceReason has a single value),
                    // so clear it unconditionally here — leaving it set is stale audit data that can
                    // mislead enforcement/monitoring tooling that reads GraceEndsAt.
                    subscription.GraceEndsAt        = null;
                    subscription.SeatGraceStartedAt = null;
                    subscription.SeatGraceReason    = null;
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

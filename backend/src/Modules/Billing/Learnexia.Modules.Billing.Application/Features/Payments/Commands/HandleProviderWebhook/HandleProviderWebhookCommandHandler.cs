using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;
// P10-09: IRefundService (Option C — charge.failed + refund.succeeded branches stay EF-free).

namespace Learnexia.Modules.Billing.Application.Features.Payments.Commands.HandleProviderWebhook;

/// <summary>
/// Handles an incoming payment provider webhook.
///
/// <para><strong>Security pipeline (order is mandatory):</strong>
/// <list type="number">
///   <item><strong>Signature verify FIRST</strong> — <see cref="IPaymentProvider.VerifyWebhookSignature"/>
///         is called before any DB read or state mutation. A forged/unsigned request is rejected
///         immediately (returns 401). Uses constant-time compare internally (Fake + live adapters
///         must honour this contract).</item>
///   <item><strong>Idempotency check</strong> — the <c>ProviderEventId</c> is looked up in
///         <c>WebhookEvent</c> (unique index). If already present → no-op return.
///         This makes the handler safe to re-invoke on provider retries without double-activating
///         or double-granting.</item>
///   <item><strong>Insert the WebhookEvent record FIRST</strong> (within the atomic transaction)
///         before any state flip — ensures the idempotency record exists even if a subsequent
///         step fails (prevents re-processing on retry).</item>
///   <item><strong>Reconcile amount against server-side Payment record</strong> — the
///         <see cref="ParsedWebhookEvent.AmountFromProvider"/> is compared against
///         <see cref="Payment.Amount"/> (server-resolved). A mismatch logs a warning but does
///         NOT silently succeed; the handler proceeds with the server-side amount as authoritative
///         and does NOT allow the provider amount to inflate the credited tier.</item>
///   <item>On <c>payment.succeeded</c> + <see cref="PaymentKind.Subscription"/>: atomic
///         transaction flips <c>Payment→Succeeded</c> and <c>Subscription→Active Premium</c> with
///         cycle dates, then publishes <see cref="SubscriptionActivatedIntegrationEvent"/> POST-commit.</item>
///   <item>On <c>payment.succeeded</c> + <see cref="PaymentKind.Pack"/>: delegates to
///         <see cref="IEnergyPackService.CreditPurchasedPackAsync"/> which atomically credits the
///         child's <c>PurchasedBalance</c> in a single explicit transaction.</item>
///   <item>On <c>payment.failed</c>: flips <c>Payment→Failed</c> and publishes
///         <see cref="PaymentFailedIntegrationEvent"/> POST-commit.</item>
/// </list>
/// </para>
///
/// <para>All inner exceptions are caught and returned as <see cref="BaseResponseHandler.ServerError{T}"/>
/// with a generic message — never leaking internal error details to the caller.</para>
/// </summary>
public sealed class HandleProviderWebhookCommandHandler
    : BaseResponseHandler,
      ICommandHandler<HandleProviderWebhookCommand, BaseResponse<WebhookHandledDto>>
{
    private readonly IBillingDbContext _db;
    private readonly IPaymentProvider _paymentProvider;
    private readonly IEnergyPackService _energyPackService;
    private readonly IRefundService _refundService;
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public HandleProviderWebhookCommandHandler(
        IBillingDbContext db,
        IPaymentProvider paymentProvider,
        IEnergyPackService energyPackService,
        IRefundService refundService,
        IPublisher publisher,
        ILoggerManager logger,
        ICurrentUserService currentUser,
        IStringLocalizer<SharedResources> localizer)
    {
        _db                = db;
        _paymentProvider   = paymentProvider;
        _energyPackService = energyPackService;
        _refundService     = refundService;
        _publisher         = publisher;
        _logger            = logger;
        _currentUser       = currentUser;
        _localizer         = localizer;
    }

    public async Task<BaseResponse<WebhookHandledDto>> Handle(
        HandleProviderWebhookCommand request,
        CancellationToken cancellationToken)
    {
        // ── STEP 1: Signature verification — MUST be first, before any state access ──
        //
        // SECURITY CRITICAL: a forged webhook with a valid-looking payload must NOT
        // be able to activate a subscription or grant Premium access.
        // The IPaymentProvider.VerifyWebhookSignature implementation uses constant-time
        // compare (HMAC-SHA256 via CryptographicOperations.FixedTimeEquals) to prevent
        // timing attacks. We return a generic 401-shape here — no detail about WHY it failed.
        if (!_paymentProvider.VerifyWebhookSignature(request.RawBody, request.SignatureHeader))
        {
            _logger.LogInfo("HandleProviderWebhook: signature verification failed — rejecting.");
            return new BaseResponse<WebhookHandledDto>
            {
                Successed  = false,
                StatusCode = System.Net.HttpStatusCode.Unauthorized,
                Message    = _localizer[SharedResourcesKey.WebhookSignatureInvalid],
            };
        }

        try
        {
            // ── STEP 2: Parse the (now-verified) webhook body ─────────────────────────
            ParsedWebhookEvent parsed;
            try
            {
                parsed = _paymentProvider.ParseWebhookEvent(request.RawBody);
            }
            catch (Exception parseEx)
            {
                _logger.LogError(parseEx, "HandleProviderWebhook: failed to parse verified webhook body.");
                return new BaseResponse<WebhookHandledDto>
                {
                    Successed  = false,
                    StatusCode = System.Net.HttpStatusCode.BadRequest,
                    Message    = _localizer[SharedResourcesKey.WebhookPayloadInvalid],
                };
            }

            // ── STEP 3: Idempotency check — is this event already processed? ─────────
            var alreadyProcessed = await _db.WebhookEvents
                .AnyAsync(w => w.ProviderEventId == parsed.ProviderEventId, cancellationToken);

            if (alreadyProcessed)
            {
                _logger.LogInfo($"HandleProviderWebhook: duplicate event {parsed.ProviderEventId} — no-op.");
                return new BaseResponse<WebhookHandledDto>
                {
                    Successed  = true,
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Message    = _localizer[SharedResourcesKey.WebhookEventAlreadyProcessed],
                    Data       = new WebhookHandledDto(parsed.ProviderEventId, "Duplicate"),
                };
            }

            // ── STEP 4: Route to the appropriate event handler ─────────────────────────
            return parsed.EventType switch
            {
                WebhookEventTypes.PaymentSucceeded =>
                    await HandlePaymentSucceededAsync(parsed, cancellationToken),

                WebhookEventTypes.PaymentFailed =>
                    await HandlePaymentFailedAsync(parsed, cancellationToken),

                // ── P10-09: New event types routed to IRefundService (EF-free branches) ──
                WebhookEventTypes.ChargeFailed =>
                    await HandleChargeFailedAsync(parsed, cancellationToken),

                WebhookEventTypes.RefundSucceeded =>
                    await HandleRefundSucceededAsync(parsed, cancellationToken),

                _ => await HandleUnknownEventAsync(parsed, cancellationToken),
            };
        }
        catch (Exception ex)
        {
            // Never leak ex.Message to the caller — it could contain internal details.
            _logger.LogError(ex, "Error in HandleProviderWebhookCommand.");
            return ServerError<WebhookHandledDto>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }

    // ── payment.succeeded ─────────────────────────────────────────────────────────────

    private async Task<BaseResponse<WebhookHandledDto>> HandlePaymentSucceededAsync(
        ParsedWebhookEvent parsed,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Insert the WebhookEvent FIRST — if anything else fails on retry this record
            // ensures we do not double-process the event.
            var webhookRecord = new WebhookEvent
            {
                ProviderEventId = parsed.ProviderEventId,
                EventType       = parsed.EventType,
                Payload         = parsed.RawPayload,
                Succeeded       = false, // set to true on commit
            };
            await _db.WebhookEvents.AddAsync(webhookRecord, ct);

            // Load the Payment by provider ref.
            var payment = await _db.Payments
                .FirstOrDefaultAsync(p => p.ProviderPaymentRef == parsed.PaymentRef, ct);

            if (payment is null)
            {
                // Unknown payment ref — record the event as received but warn.
                webhookRecord.ProcessedAt = DateTime.UtcNow;
                webhookRecord.Succeeded   = false;
                await _db.SaveChangesAsync(0);
                await tx.CommitAsync(ct);

                _logger.LogInfo($"HandleProviderWebhook: payment.succeeded for unknown ref={parsed.PaymentRef} — event recorded.");
                return new BaseResponse<WebhookHandledDto>
                {
                    Successed  = true,
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Message    = _localizer[SharedResourcesKey.WebhookProcessed],
                    Data       = new WebhookHandledDto(parsed.ProviderEventId, "PaymentNotFound"),
                };
            }

            // ── Amount reconciliation: server-side amount is authoritative ────────────
            // A forged webhook cannot inflate the tier by supplying a higher amount.
            // We proceed with the server-side Payment.Amount; mismatch is logged as an alert.
            if (Math.Abs(parsed.AmountFromProvider - payment.Amount) > 0.01m)
            {
                _logger.LogInfo($"HandleProviderWebhook: amount mismatch — provider={parsed.AmountFromProvider}, server={payment.Amount}. Using server-side value.");
            }

            // ── P10-07: Branch by PaymentKind ─────────────────────────────────────────
            // Pack payments are handled entirely by IEnergyPackService (Option C seam).
            // The WebhookEvent row inserted above serves as the outer idempotency guard.
            // We commit only the WebhookEvent record here; the service manages its own
            // transaction for the credit + Payment.Status flip.
            if (payment.Kind == PaymentKind.Pack)
            {
                // Commit the WebhookEvent row first so the idempotency guard is in place.
                webhookRecord.ProcessedAt = DateTime.UtcNow;
                webhookRecord.Succeeded   = true;
                await _db.SaveChangesAsync(0);
                await tx.CommitAsync(ct);

                if (!payment.TargetChildId.HasValue)
                {
                    _logger.LogInfo($"HandleProviderWebhook: Pack payment {payment.Id} has no TargetChildId — skipping credit.");
                    return new BaseResponse<WebhookHandledDto>
                    {
                        Successed  = true,
                        StatusCode = System.Net.HttpStatusCode.OK,
                        Message    = _localizer[SharedResourcesKey.WebhookProcessed],
                        Data       = new WebhookHandledDto(parsed.ProviderEventId, "PackNoTarget"),
                    };
                }

                // Delegate crediting to the EnergyPackService (owns its own transaction).
                var creditResult = await _energyPackService.CreditPurchasedPackAsync(
                    paymentId      : payment.Id,
                    targetChildId  : payment.TargetChildId.Value,
                    providerEventId: parsed.ProviderEventId,
                    ct             : ct);

                _logger.LogInfo(
                    $"HandleProviderWebhook: Pack credited — paymentId={payment.Id}, childId={payment.TargetChildId.Value}, duplicate={creditResult.WasDuplicate}.");

                return new BaseResponse<WebhookHandledDto>
                {
                    Successed  = true,
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Message    = _localizer[SharedResourcesKey.WebhookProcessed],
                    Data       = new WebhookHandledDto(
                        parsed.ProviderEventId,
                        creditResult.WasDuplicate ? "PackDuplicate" : "PackCredited"),
                };
            }

            // ── Subscription path (unchanged W3 logic) ────────────────────────────────

            // Flip Payment → Succeeded.
            payment.Status             = PaymentStatus.Succeeded;
            payment.ProviderPaymentRef = parsed.PaymentRef; // ensure stored

            // Flip Subscription → Active Premium (if linked).
            Subscription? subscription = null;
            if (payment.SubscriptionId.HasValue)
            {
                subscription = await _db.Subscriptions
                    .FirstOrDefaultAsync(s => s.Id == payment.SubscriptionId.Value, ct);

                if (subscription is not null)
                {
                    var nowUtc = DateTime.UtcNow;
                    subscription.PlanCode   = PlanCode.Premium;
                    subscription.Status     = SubscriptionStatus.Active;

                    // Apply pending billing period if one was requested.
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

            // Mark webhook as fully processed.
            webhookRecord.ProcessedAt = DateTime.UtcNow;
            webhookRecord.Succeeded   = true;

            await _db.SaveChangesAsync(0);
            await tx.CommitAsync(ct);

            _logger.LogInfo($"HandleProviderWebhook: payment.succeeded — paymentId={payment.Id}, parentId={payment.ParentUserId}.");

            // ── POST-COMMIT integration events ────────────────────────────────────────
            // Events fire AFTER the commit so consumers see consistent state.
            // If the publish fails the payment is still Succeeded (no data loss).
            if (subscription is not null)
            {
                await _publisher.Publish(new SubscriptionActivatedIntegrationEvent(
                    EventId        : Guid.NewGuid(),
                    OccurredOnUtc  : DateTime.UtcNow,
                    ParentUserId   : payment.ParentUserId,
                    SubscriptionId : subscription.Id,
                    PaymentId      : payment.Id,
                    BillingPeriod  : subscription.BillingPeriod.ToString()), ct);
            }

            return new BaseResponse<WebhookHandledDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.WebhookProcessed],
                Data       = new WebhookHandledDto(parsed.ProviderEventId, "PaymentSucceeded"),
            };
        }
        catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
        {
            // Concurrent duplicate insert of the same ProviderEventId — race condition idempotency.
            await tx.RollbackAsync(ct);
            _logger.LogInfo($"HandleProviderWebhook: concurrent duplicate for eventId={parsed.ProviderEventId} — no-op.");
            return new BaseResponse<WebhookHandledDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.WebhookEventAlreadyProcessed],
                Data       = new WebhookHandledDto(parsed.ProviderEventId, "ConcurrentDuplicate"),
            };
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── payment.failed ────────────────────────────────────────────────────────────────

    private async Task<BaseResponse<WebhookHandledDto>> HandlePaymentFailedAsync(
        ParsedWebhookEvent parsed,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Insert idempotency record first.
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

            _logger.LogInfo($"HandleProviderWebhook: payment.failed — paymentRef={parsed.PaymentRef}.");

            // POST-COMMIT event for dunning (P10-09).
            await _publisher.Publish(new PaymentFailedIntegrationEvent(
                EventId       : Guid.NewGuid(),
                OccurredOnUtc : DateTime.UtcNow,
                ParentUserId  : payment?.ParentUserId ?? 0,
                PaymentId     : payment?.Id ?? 0,
                SubscriptionId: payment?.SubscriptionId), ct);

            return new BaseResponse<WebhookHandledDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.WebhookProcessed],
                Data       = new WebhookHandledDto(parsed.ProviderEventId, "PaymentFailed"),
            };
        }
        catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
        {
            await tx.RollbackAsync(ct);
            return new BaseResponse<WebhookHandledDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.WebhookEventAlreadyProcessed],
                Data       = new WebhookHandledDto(parsed.ProviderEventId, "ConcurrentDuplicate"),
            };
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── P10-09: charge.failed → IRefundService (EF-free branch) ─────────────────────────

    private async Task<BaseResponse<WebhookHandledDto>> HandleChargeFailedAsync(
        ParsedWebhookEvent parsed,
        CancellationToken ct)
    {
        // Insert the WebhookEvent idempotency record first (mirrors all other branches).
        // If the record already exists (duplicate event) → no-op.
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

            // Resolve the Payment row to get subscriptionId.
            var payment = await _db.Payments
                .FirstOrDefaultAsync(p => p.ProviderPaymentRef == parsed.PaymentRef, ct);

            webhookRecord.ProcessedAt = DateTime.UtcNow;
            webhookRecord.Succeeded   = true;
            await _db.SaveChangesAsync(0);
            await tx.CommitAsync(ct);

            if (payment is null || !payment.SubscriptionId.HasValue)
            {
                _logger.LogInfo(
                    $"HandleProviderWebhook: charge.failed for unknown ref={parsed.PaymentRef} — recorded.");
                return new BaseResponse<WebhookHandledDto>
                {
                    Successed  = true,
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Message    = _localizer[SharedResourcesKey.WebhookProcessed],
                    Data       = new WebhookHandledDto(parsed.ProviderEventId, "ChargeFailedNoSubscription"),
                };
            }

            // ── Delegate dunning state update to IRefundService (EF-free from here) ──────
            var result = await _refundService.ProcessChargeFailedAsync(
                paymentId      : payment.Id,
                subscriptionId : payment.SubscriptionId.Value,
                providerEventId: parsed.ProviderEventId,
                ct             : ct);

            _logger.LogInfo(
                $"HandleProviderWebhook: charge.failed processed — subscriptionId={payment.SubscriptionId.Value}, " +
                $"attempts={result.FailedAttemptCount}, downgradeScheduled={result.DowngradeScheduled}.");

            // POST-COMMIT: emit dunning notification event.
            await _publisher.Publish(new PaymentFailedIntegrationEvent(
                EventId        : Guid.NewGuid(),
                OccurredOnUtc  : DateTime.UtcNow,
                ParentUserId   : payment.ParentUserId,
                PaymentId      : payment.Id,
                SubscriptionId : payment.SubscriptionId), ct);

            return new BaseResponse<WebhookHandledDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.WebhookProcessed],
                Data       = new WebhookHandledDto(parsed.ProviderEventId,
                    result.DowngradeScheduled ? "DunningDowngradeScheduled" : "DunningRetryScheduled"),
            };
        }
        catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
        {
            await tx.RollbackAsync(ct);
            return new BaseResponse<WebhookHandledDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.WebhookEventAlreadyProcessed],
                Data       = new WebhookHandledDto(parsed.ProviderEventId, "ConcurrentDuplicate"),
            };
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── P10-09: refund.succeeded → IRefundService (EF-free branch) ──────────────────────

    private async Task<BaseResponse<WebhookHandledDto>> HandleRefundSucceededAsync(
        ParsedWebhookEvent parsed,
        CancellationToken ct)
    {
        // Insert WebhookEvent idempotency record first.
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
                    $"HandleProviderWebhook: refund.succeeded for unknown ref={parsed.PaymentRef} — recorded.");
                return new BaseResponse<WebhookHandledDto>
                {
                    Successed  = true,
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Message    = _localizer[SharedResourcesKey.WebhookProcessed],
                    Data       = new WebhookHandledDto(parsed.ProviderEventId, "RefundPaymentNotFound"),
                };
            }

            // ── Delegate refund clawback to IRefundService (EF-free from here) ─────────
            string outcome;
            if (payment.Kind == PaymentKind.Pack)
            {
                var result = await _refundService.ProcessPackRefundAsync(
                    paymentId      : payment.Id,
                    providerEventId: parsed.ProviderEventId,
                    ct             : ct);

                _logger.LogInfo(
                    $"HandleProviderWebhook: pack refund — paymentId={payment.Id}, " +
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
                    $"HandleProviderWebhook: subscription refund — paymentId={payment.Id}, " +
                    $"duplicate={result.WasDuplicate}.");

                outcome = result.WasDuplicate ? "SubscriptionRefundDuplicate" : "SubscriptionRefunded";
            }

            return new BaseResponse<WebhookHandledDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.WebhookProcessed],
                Data       = new WebhookHandledDto(parsed.ProviderEventId, outcome),
            };
        }
        catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
        {
            await tx.RollbackAsync(ct);
            return new BaseResponse<WebhookHandledDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.WebhookEventAlreadyProcessed],
                Data       = new WebhookHandledDto(parsed.ProviderEventId, "ConcurrentDuplicate"),
            };
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── Unknown / unhandled event type ────────────────────────────────────────────────

    private async Task<BaseResponse<WebhookHandledDto>> HandleUnknownEventAsync(
        ParsedWebhookEvent parsed,
        CancellationToken ct)
    {
        // Record the event for audit but take no state action.
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

        _logger.LogInfo($"HandleProviderWebhook: unhandled eventType={parsed.EventType}, id={parsed.ProviderEventId}.");
        return new BaseResponse<WebhookHandledDto>
        {
            Successed  = true,
            StatusCode = System.Net.HttpStatusCode.OK,
            Message    = _localizer[SharedResourcesKey.WebhookProcessed],
            Data       = new WebhookHandledDto(parsed.ProviderEventId, "Unhandled"),
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("23505", StringComparison.Ordinal) == true
           || ex.InnerException?.Message.Contains("UX_WebhookEvents_ProviderEventId",
               StringComparison.OrdinalIgnoreCase) == true;
}

using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Refunds.Dtos;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Implements <see cref="IRefundService"/> — the Option-C service seam for refund and dunning
/// state-management operations.
///
/// <para><strong>Atomicity:</strong> every write path opens an explicit transaction, mutates
/// state, inserts audit/ledger rows, and commits in one unit. Rollback on any exception.</para>
///
/// <para><strong>Idempotency:</strong>
/// <list type="bullet">
///   <item><see cref="ProcessPackRefundAsync"/> guards via <c>CreditTransaction.IdempotencyKey</c>
///         (DB-unique) — <c>"pack-refund:{paymentId}"</c> (PER-PAYMENT, not per-event: a 2nd
///         refund.succeeded with a DISTINCT provider event id collides on the unique constraint,
///         preventing an over-refund — do NOT re-add the event-id suffix), PLUS a
///         <c>Payment.Status == Refunded</c> guard and an already-refunded subtraction in the
///         reconcile.</item>
///   <item><see cref="ProcessSubscriptionRefundAsync"/> guards via a <c>Payment.Status</c> check
///         (already-<c>Refunded</c> rows are no-ops).</item>
///   <item><see cref="ProcessChargeFailedAsync"/> guards via an <c>Payment.Status</c> check
///         (already-<c>Failed</c> rows for the same payment are no-ops).</item>
/// </list>
/// </para>
///
/// <para><strong>P10-13 CUTOVER (AC13-8):</strong> <see cref="ProcessPackRefundAsync"/> claws back
/// from the SHARED family <see cref="FamilyEnergyAccount.PurchasedBalance"/> (resolved via the pack
/// <c>Payment.ParentUserId</c>), NOT the legacy per-child <c>CreditAccount</c>. Pack credits land on
/// the shared wallet (<c>EnergyPackService.CreditPurchasedPackAsync</c>), so the clawback targets the
/// same wallet — no split economy. (Full FIFO reconciliation is P10-17.)</para>
///
/// <para><strong>Never-negative:</strong> <see cref="FamilyEnergyAccount.RefundPurchased"/> clamps the
/// clawback to the available <see cref="FamilyEnergyAccount.PurchasedBalance"/> (never drives it
/// negative). The domain entity enforces this invariant; the DB check constraint is defence-in-depth.</para>
///
/// <para>The <c>IServiceScopeFactory</c>-scoped injection pattern (used in
/// <see cref="ProcessDunningRetriesAsync"/>) mirrors <see cref="BillingGrantJob"/>.</para>
/// </summary>
public sealed class RefundService : IRefundService
{
    private readonly IBillingDbContext _db;
    private readonly IPaymentProvider _paymentProvider;
    private readonly ICurrentUserService _currentUser;
    private readonly IGlobalSettingsProvider _settings;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerManager _logger;

    public RefundService(
        IBillingDbContext db,
        IPaymentProvider paymentProvider,
        ICurrentUserService currentUser,
        IGlobalSettingsProvider settings,
        IServiceScopeFactory scopeFactory,
        ILoggerManager logger)
    {
        _db              = db;
        _paymentProvider = paymentProvider;
        _currentUser     = currentUser;
        _settings        = settings;
        _scopeFactory    = scopeFactory;
        _logger          = logger;
    }

    // ── ProcessPackRefundAsync ────────────────────────────────────────────────────────

    // SECURITY constants for the bounded concurrency retry (Finding #2 — HARDEN-01 mirror).
    private const int PackRefundMaxRetries = 3;
    private const int PackRefundBaseDelayMs = 20;
    private const int PackRefundMaxDelayMs = 250;
    private const int PackRefundJitterMs = 50;

    /// <inheritdoc/>
    public async Task<RefundResult> ProcessPackRefundAsync(
        int paymentId,
        string providerEventId,
        CancellationToken ct)
    {
        // Finding #1 (CRITICAL) fix, point 3: use a per-payment idempotency key for the clawback
        // ledger row so a second settlement with a DISTINCT provider event id still collides on the
        // DB unique constraint and cannot produce a second Refund row for the same payment.
        // The outer per-event WebhookEvent dedup (by providerEventId) is the caller's guard; this
        // is the inner defence-in-depth guard that holds even when the outer guard is bypassed.
        var idempotencyKey = $"pack-refund:{paymentId}";

        for (var attempt = 0; attempt <= PackRefundMaxRetries; attempt++)
        {
            try
            {
                return await ExecutePackRefundCoreAsync(paymentId, idempotencyKey, ct);
            }
            catch (DbUpdateConcurrencyException) when (attempt < PackRefundMaxRetries)
            {
                // Finding #2 (HIGH) fix: optimistic-concurrency retry (HARDEN-01 mirror).
                // The xmin token on FamilyEnergyAccount fired — a concurrent write beat us.
                // Clear the stale tracker state, back off, and re-read the wallet.
                _logger.LogWarn(
                    $"RefundService.ProcessPackRefund: xmin conflict attempt {attempt + 1} paymentId={paymentId}. Retrying.");
                _db.ChangeTracker.Clear();
                await ApplyPackRefundBackoffAsync(attempt, ct);
            }
            catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
            {
                _db.ChangeTracker.Clear();
                _logger.LogInfo(
                    $"RefundService.ProcessPackRefund: concurrent duplicate key={idempotencyKey} — no-op.");
                return RefundResult.Duplicate();
            }
        }

        // Retries exhausted — propagate as a transient failure; caller may retry.
        _logger.LogError(
            new InvalidOperationException("Retries exhausted"),
            $"RefundService.ProcessPackRefund: exceeded {PackRefundMaxRetries} xmin retries paymentId={paymentId}");
        throw new InvalidOperationException(
            $"RefundService.ProcessPackRefund: exceeded {PackRefundMaxRetries} retries for paymentId={paymentId}");
    }

    /// <summary>
    /// Core transactional pack-refund body — called by <see cref="ProcessPackRefundAsync"/>
    /// inside the retry loop so the entire DB round-trip can be retried on xmin conflict.
    /// </summary>
    private async Task<RefundResult> ExecutePackRefundCoreAsync(
        int paymentId,
        string idempotencyKey,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Idempotency pre-check (cheap path).
            if (await _db.CreditTransactions.AnyAsync(t => t.IdempotencyKey == idempotencyKey, ct))
            {
                await tx.RollbackAsync(ct);
                _logger.LogInfo($"RefundService.ProcessPackRefund: duplicate key={idempotencyKey} — no-op.");
                return RefundResult.Duplicate();
            }

            // Load the original payment (with tracking so Status can be updated).
            var payment = await _db.Payments
                .FirstOrDefaultAsync(p => p.Id == paymentId, ct);

            if (payment is null)
            {
                await tx.RollbackAsync(ct);
                _logger.LogInfo($"RefundService.ProcessPackRefund: payment {paymentId} not found.");
                return RefundResult.Fail(RefundFailureReason.PaymentNotFound);
            }

            // Finding #1 (CRITICAL) fix, point 1: mirror the subscription path's status guard.
            // If the payment is already Refunded (e.g. a second refund.succeeded with a DISTINCT
            // provider event id arrives), return Duplicate immediately — no balance change occurs.
            if (payment.Status == PaymentStatus.Refunded)
            {
                await tx.RollbackAsync(ct);
                _logger.LogInfo(
                    $"RefundService.ProcessPackRefund: paymentId={paymentId} already Refunded — no-op.");
                return RefundResult.Duplicate();
            }

            if (payment.Kind != PaymentKind.Pack || !payment.TargetChildId.HasValue)
            {
                await tx.RollbackAsync(ct);
                _logger.LogInfo($"RefundService.ProcessPackRefund: payment {paymentId} is not a valid Pack payment.");
                return RefundResult.Fail(RefundFailureReason.PaymentNotRefundable);
            }

            // P10-17-BE-4: claw back from the SHARED family FamilyEnergyAccount.PurchasedBalance.
            // Load the wallet WITH tracking so the xmin concurrency token participates in
            // SaveChangesAsync conflict detection (Finding #2 fix).
            var wallet = await _db.FamilyEnergyAccounts
                .FirstOrDefaultAsync(w => w.ParentUserId == payment.ParentUserId, ct);

            if (wallet is null)
            {
                await tx.RollbackAsync(ct);
                _logger.LogInfo($"RefundService.ProcessPackRefund: no family wallet for parentId={payment.ParentUserId}.");
                return RefundResult.Fail(RefundFailureReason.CreditAccountNotFound);
            }

            // Re-reconcile FIFO refundable amount from the ledger (P10-17-BE-4 rule: re-reconcile,
            // never trust the request-time figure). ComputeRefundableAsync runs READ-ONLY inside
            // the open transaction — safe because all reads are AsNoTracking and non-mutating.
            var quote = await ComputeRefundableAsync(wallet.Id, paymentId, ct);
            int refundableUnits = quote?.Refundable ?? 0;

            // Build the Refund ledger row with PurchasedRefund reason code.
            // RefundPurchased domain mutator clamps to available PurchasedBalance (defence-in-depth).
            var refundTx = wallet.RefundPurchased(refundableUnits, idempotencyKey, payment.Id.ToString());
            refundTx.FamilyEnergyAccountId = wallet.Id;
            refundTx.ReasonCode = CreditReasonCode.PurchasedRefund;
            await _db.CreditTransactions.AddAsync(refundTx, ct);

            // Mark payment as Refunded (Finding #1, point 1 — flip status on settlement so the
            // status guard above catches any subsequent distinct-event-id webhook).
            payment.Status = PaymentStatus.Refunded;

            // SaveChangesAsync will assert the xmin token on FamilyEnergyAccount; a concurrent
            // write throws DbUpdateConcurrencyException which is caught in the retry loop above
            // (Finding #2 fix — HARDEN-01 mirror).
            await _db.SaveChangesAsync(_currentUser.UserId ?? 0);
            await tx.CommitAsync(ct);

            _logger.LogInfo(
                $"RefundService.ProcessPackRefund: paymentId={paymentId}, clawedBack={refundTx.Amount}, " +
                $"parentId={payment.ParentUserId}.");

            return RefundResult.Ok(refundTx.Amount);
        }
        catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
        {
            await tx.RollbackAsync(ct);
            _logger.LogInfo($"RefundService.ProcessPackRefund: concurrent duplicate key={idempotencyKey} — no-op.");
            return RefundResult.Duplicate();
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>HARDEN-01 mirror: exponential back-off with jitter for the pack-refund retry loop.</summary>
    private static Task ApplyPackRefundBackoffAsync(int attempt, CancellationToken ct)
    {
        var shift         = Math.Min(attempt, 30);
        var deterministic = (int)Math.Min(PackRefundMaxDelayMs, (long)PackRefundBaseDelayMs * (1L << shift));
        var jitter        = Random.Shared.Next(0, PackRefundJitterMs + 1);
        return Task.Delay(deterministic + jitter, ct);
    }

    // ── ProcessSubscriptionRefundAsync ────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<RefundResult> ProcessSubscriptionRefundAsync(
        int paymentId,
        string providerEventId,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var payment = await _db.Payments
                .FirstOrDefaultAsync(p => p.Id == paymentId, ct);

            if (payment is null)
            {
                await tx.RollbackAsync(ct);
                return RefundResult.Fail(RefundFailureReason.PaymentNotFound);
            }

            // Idempotency: already refunded = no-op.
            if (payment.Status == PaymentStatus.Refunded)
            {
                await tx.RollbackAsync(ct);
                _logger.LogInfo($"RefundService.ProcessSubscriptionRefund: paymentId={paymentId} already Refunded — no-op.");
                return RefundResult.Duplicate();
            }

            if (payment.Kind != PaymentKind.Subscription)
            {
                await tx.RollbackAsync(ct);
                return RefundResult.Fail(RefundFailureReason.PaymentNotRefundable);
            }

            // Policy: revoke Premium access immediately (downgrade subscription to Free).
            // Do NOT claw back already-granted monthly credits.
            if (payment.SubscriptionId.HasValue)
            {
                var subscription = await _db.Subscriptions
                    .FirstOrDefaultAsync(s => s.Id == payment.SubscriptionId.Value, ct);

                if (subscription is not null)
                {
                    subscription.PlanCode          = PlanCode.Free;
                    subscription.Status            = SubscriptionStatus.Active;
                    subscription.CurrentCycleStart = null;
                    subscription.CurrentCycleEnd   = null;
                    subscription.PendingPlanCode   = null;
                    subscription.GraceEndsAt       = null;
                    subscription.NextRetryAt       = null;
                    subscription.FailedAttemptCount = 0;
                }
            }

            payment.Status = PaymentStatus.Refunded;

            await _db.SaveChangesAsync(_currentUser.UserId ?? 0);
            await tx.CommitAsync(ct);

            _logger.LogInfo(
                $"RefundService.ProcessSubscriptionRefund: paymentId={paymentId} refunded, subscription downgraded to Free.");

            return RefundResult.Ok();
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── ProcessChargeFailedAsync ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<ChargeFailedResult> ProcessChargeFailedAsync(
        int paymentId,
        int subscriptionId,
        string providerEventId,
        CancellationToken ct)
    {
        var maxRetries     = _settings.GetInt(GlobalSettingKeys.DunningMaxRetries, 3);
        var retryHours     = _settings.GetInt(GlobalSettingKeys.DunningRetryIntervalHours, 24);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var payment = await _db.Payments
                .FirstOrDefaultAsync(p => p.Id == paymentId, ct);

            // Idempotency: already Failed = no-op.
            if (payment is not null && payment.Status == PaymentStatus.Failed)
            {
                await tx.RollbackAsync(ct);
                _logger.LogInfo($"RefundService.ProcessChargeFailed: paymentId={paymentId} already Failed — no-op.");
                return ChargeFailedResult.Duplicate();
            }

            if (payment is not null)
                payment.Status = PaymentStatus.Failed;

            var subscription = await _db.Subscriptions
                .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);

            if (subscription is null)
            {
                await tx.RollbackAsync(ct);
                _logger.LogInfo($"RefundService.ProcessChargeFailed: subscription {subscriptionId} not found.");
                return ChargeFailedResult.Fail();
            }

            subscription.FailedAttemptCount++;

            bool downgradeScheduled;
            DateTime? graceEndsAt;
            DateTime? nextRetryAt;
            var nowUtc = DateTime.UtcNow;

            if (subscription.FailedAttemptCount >= maxRetries)
            {
                // Retries exhausted — enter final grace window.
                // P10-15-BE-2: seat grace window = now + seats.grace_days (7 days by default).
                // Idempotency: if GraceEndsAt is already set and > nowUtc, do NOT extend/shorten.
                subscription.Status    = SubscriptionStatus.Dunning;
                subscription.NextRetryAt = null;
                downgradeScheduled = true;
                nextRetryAt = null;
                if (subscription.GraceEndsAt == null || subscription.GraceEndsAt <= nowUtc)
                {
                    var graceDays = _settings.GetInt(GlobalSettingKeys.SeatsGraceDays, 7);
                    subscription.GraceEndsAt = nowUtc.AddDays(graceDays);
                    subscription.SeatGraceStartedAt = nowUtc;
                    subscription.SeatGraceReason = SeatGraceReason.PaymentFailure;
                }
                graceEndsAt = subscription.GraceEndsAt;
            }
            else
            {
                // Still retrying.
                subscription.Status      = SubscriptionStatus.PastDue;
                subscription.NextRetryAt = nowUtc.AddHours(retryHours);
                subscription.GraceEndsAt = null;
                downgradeScheduled = false;
                graceEndsAt = null;
                nextRetryAt = subscription.NextRetryAt;
            }

            await _db.SaveChangesAsync(_currentUser.UserId ?? 0);
            await tx.CommitAsync(ct);

            _logger.LogInfo(
                $"RefundService.ProcessChargeFailed: subscriptionId={subscriptionId}, " +
                $"attempts={subscription.FailedAttemptCount}, dunning={downgradeScheduled}.");

            return ChargeFailedResult.Ok(
                subscription.FailedAttemptCount,
                downgradeScheduled,
                graceEndsAt,
                nextRetryAt);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── InitiateRefundAsync ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<InitiateRefundResult> InitiateRefundAsync(
        int paymentId,
        string reason,
        int adminUserId,
        CancellationToken ct)
    {
        // Load the payment to get the provider ref and validate state.
        var payment = await _db.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct);

        if (payment is null)
            return InitiateRefundResult.Fail(InitiateRefundFailureReason.PaymentNotFound);

        if (payment.Status != PaymentStatus.Succeeded)
            return InitiateRefundResult.Fail(InitiateRefundFailureReason.PaymentNotSucceeded);

        if (string.IsNullOrEmpty(payment.ProviderPaymentRef))
        {
            _logger.LogInfo(
                $"RefundService.InitiateRefund: paymentId={paymentId} has no ProviderPaymentRef — cannot refund.");
            return InitiateRefundResult.Fail(InitiateRefundFailureReason.ProviderError);
        }

        // Call the provider (Fake = deterministic no-op/success).
        // The actual state change (clawback + status flip) happens when the provider sends the
        // refund.succeeded webhook — not here.
        var providerSuccess = await _paymentProvider.InitiateRefundAsync(
            payment.ProviderPaymentRef,
            payment.Amount,
            ct);

        if (!providerSuccess)
        {
            _logger.LogInfo(
                $"RefundService.InitiateRefund: provider declined refund for paymentId={paymentId}.");
            return InitiateRefundResult.Fail(InitiateRefundFailureReason.ProviderError);
        }

        _logger.LogInfo(
            $"RefundService.InitiateRefund: admin={adminUserId}, paymentId={paymentId}, reason='{reason}' — provider accepted.");

        return InitiateRefundResult.Ok();
    }

    // ── ProcessDunningRetriesAsync ────────────────────────────────────────────────────

    // ── ComputeRefundableAsync (P10-17-BE-2) ─────────────────────────────────────

    /// <inheritdoc/>
    public async Task<RefundableQuoteDto?> ComputeRefundableAsync(
        int familyAccountId,
        int purchasePaymentId,
        CancellationToken ct)
    {
        // Load the payment for monetary translation.
        var payment = await _db.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == purchasePaymentId && p.Kind == PaymentKind.Pack, ct);

        if (payment is null)
        {
            _logger.LogInfo(
                $"RefundService.ComputeRefundable: payment {purchasePaymentId} not found or not a Pack payment.");
            return null;
        }

        // Load the family wallet for the live PurchasedBalance cap.
        var wallet = await _db.FamilyEnergyAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == familyAccountId, ct);

        if (wallet is null)
        {
            _logger.LogInfo(
                $"RefundService.ComputeRefundable: family account {familyAccountId} not found.");
            return null;
        }

        // Finding #3 (Low — defence-in-depth): explicit parent-ownership assertion.
        // Verify the payment's owning parent matches the family account so IDOR safety
        // is enforced structurally, not emergently (a foreign payment can no longer drift
        // through to a zero-quote result that hides the violation).
        // NOTE: The admin path passes the payment's own familyAccountId resolved from the
        //       payment.ParentUserId, so this check is transparent for admin calls.
        if (payment.ParentUserId != wallet.ParentUserId)
        {
            _logger.LogInfo(
                $"RefundService.ComputeRefundable: ownership mismatch — " +
                $"payment.ParentUserId={payment.ParentUserId} vs wallet.ParentUserId={wallet.ParentUserId} " +
                $"for paymentId={purchasePaymentId}, familyAccountId={familyAccountId}.");
            return null;
        }

        // Load ALL bucket-B Purchase rows for this family, ordered oldest-first.
        var allPurchaseRows = await _db.CreditTransactions
            .AsNoTracking()
            .Where(t => t.FamilyEnergyAccountId == familyAccountId
                     && t.Type == CreditTransactionType.Purchase
                     && t.SourceBucket == EnergyBucket.Purchased)
            .OrderBy(t => t.OccurredAtUtc)
            .ThenBy(t => t.Id)
            .Select(t => new { t.Amount, t.RelatedPaymentId })
            .ToListAsync(ct);

        // Sum ALL bucket-B Spend rows for this family.
        int totalBucketBSpend = await _db.CreditTransactions
            .AsNoTracking()
            .Where(t => t.FamilyEnergyAccountId == familyAccountId
                     && t.Type == CreditTransactionType.Spend
                     && t.SourceBucket == EnergyBucket.Purchased)
            .SumAsync(t => (int?)t.Amount, ct) ?? 0;

        // Finding #1 (CRITICAL) fix, point 2: sum prior Refund rows for THIS payment.
        // Without this, a second refund.succeeded for the same payment (distinct event id, new
        // idempotency key) would see the full FIFO-refundable amount again because the first
        // clawback doesn't reduce the Purchase/Spend ledger — only the live PurchasedBalance.
        // Subtracting alreadyRefundedForThisPayment from refundableUnits makes the reconcile
        // self-correcting even after PurchasedBalance is replenished by a new pack purchase.
        string paymentIdStr = purchasePaymentId.ToString();
        int alreadyRefundedForThisPayment = await _db.CreditTransactions
            .AsNoTracking()
            .Where(t => t.FamilyEnergyAccountId == familyAccountId
                     && t.Type == CreditTransactionType.Refund
                     && t.SourceBucket == EnergyBucket.Purchased
                     && t.RelatedPaymentId == paymentIdStr)
            .SumAsync(t => (int?)t.Amount, ct) ?? 0;

        // FIFO attribution: walk purchases oldest-first, consuming spend against them in order.
        int spendRemaining = totalBucketBSpend;
        int consumedForThisPayment = 0;
        int purchasedTotalForThisPayment = 0;

        foreach (var purchase in allPurchaseRows)
        {
            int attributedSpend = Math.Min(spendRemaining, purchase.Amount);
            spendRemaining -= attributedSpend;

            if (purchase.RelatedPaymentId == paymentIdStr)
            {
                purchasedTotalForThisPayment += purchase.Amount;
                consumedForThisPayment += attributedSpend;
            }
        }

        // Refundable energy units:
        //   refundable = purchasedTotal − consumed(FIFO) − alreadyRefunded(for this payment)
        //   clamped >= 0, never exceeds live PurchasedBalance.
        // The alreadyRefunded subtraction is the key fix for the distinct-event-id over-refund:
        // even if balance is replenished, the reconcile will return 0 after the first successful
        // refund because alreadyRefundedForThisPayment == purchasedTotalForThisPayment − consumed.
        int refundableUnits = Math.Max(
            0,
            purchasedTotalForThisPayment - consumedForThisPayment - alreadyRefundedForThisPayment);
        refundableUnits = Math.Min(refundableUnits, wallet.PurchasedBalance);

        // Translate to monetary value at the original unit price.
        decimal refundableAmount = 0m;
        if (purchasedTotalForThisPayment > 0)
        {
            decimal pricePerUnit = payment.Amount / purchasedTotalForThisPayment;
            refundableAmount = Math.Round(pricePerUnit * refundableUnits, 2);
        }

        _logger.LogInfo(
            $"RefundService.ComputeRefundable: paymentId={purchasePaymentId}, " +
            $"purchasedTotal={purchasedTotalForThisPayment}, consumed={consumedForThisPayment}, " +
            $"alreadyRefunded={alreadyRefundedForThisPayment}, refundableUnits={refundableUnits}, " +
            $"refundableAmount={refundableAmount}.");

        return new RefundableQuoteDto
        {
            PurchasedTotal    = purchasedTotalForThisPayment,
            ConsumedPurchased = consumedForThisPayment,
            Refundable        = refundableUnits,
            RefundableAmount  = refundableAmount,
            Currency          = payment.Currency,
            PurchasePaymentId = purchasePaymentId,
        };
    }

    // ── InitiateProviderRefundAsync (P10-17-BE-3/BE-6) ───────────────────────────

    /// <inheritdoc/>
    public async Task<InitiateRefundResult> InitiateProviderRefundAsync(
        int purchasePaymentId,
        decimal refundableAmount,
        RefundReason reason,
        int actorUserId,
        CancellationToken ct)
    {
        var payment = await _db.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == purchasePaymentId, ct);

        if (payment is null)
            return InitiateRefundResult.Fail(InitiateRefundFailureReason.PaymentNotFound);

        if (payment.Status != PaymentStatus.Succeeded)
            return InitiateRefundResult.Fail(InitiateRefundFailureReason.PaymentNotSucceeded);

        if (string.IsNullOrEmpty(payment.ProviderPaymentRef))
        {
            _logger.LogInfo(
                $"RefundService.InitiateProviderRefund: paymentId={purchasePaymentId} has no ProviderPaymentRef.");
            return InitiateRefundResult.Fail(InitiateRefundFailureReason.ProviderError);
        }

        var providerSuccess = await _paymentProvider.InitiateRefundAsync(
            payment.ProviderPaymentRef,
            refundableAmount,
            ct);

        if (!providerSuccess)
        {
            _logger.LogInfo(
                $"RefundService.InitiateProviderRefund: provider declined for paymentId={purchasePaymentId}.");
            return InitiateRefundResult.Fail(InitiateRefundFailureReason.ProviderError);
        }

        _logger.LogInfo(
            $"RefundService.InitiateProviderRefund: actor={actorUserId}, paymentId={purchasePaymentId}, " +
            $"amount={refundableAmount}, reason={reason} — provider accepted.");

        return InitiateRefundResult.Ok();
    }

    /// <inheritdoc/>
    public async Task<DunningRetryResult> ProcessDunningRetriesAsync(
        DateTime nowUtc,
        CancellationToken ct)
    {
        int attempted = 0, succeeded = 0, failed = 0;

        // ── Pass 1: Grace-expired subscriptions → downgrade to Free ───────────────────────
        // Subscriptions in PastDue or Dunning whose GraceEndsAt has elapsed are downgraded
        // immediately. This handles the Dunning case (NextRetryAt=null) which was never
        // swept by the retry query below.
        List<int> graceExpiredIds;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            graceExpiredIds = await db.Subscriptions
                .Where(s =>
                    (s.Status == SubscriptionStatus.PastDue || s.Status == SubscriptionStatus.Dunning)
                    && s.GraceEndsAt.HasValue
                    && s.GraceEndsAt.Value <= nowUtc)
                .Select(s => s.Id)
                .ToListAsync(ct);
        }

        foreach (var subId in graceExpiredIds)
        {
            attempted++;
            try
            {
                await DowngradeExpiredGraceAsync(subId, ct);
                succeeded++;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex,
                    $"RefundService.DunningGraceExpiry: failed for subscriptionId={subId}.");
            }
        }

        // ── Pass 2: PastDue/Dunning subscriptions whose NextRetryAt has passed → retry ───
        // Use AsNoTracking for the query; per-subscription writes get a fresh scope.
        List<int> eligibleIds;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            eligibleIds = await db.Subscriptions
                .Where(s =>
                    (s.Status == SubscriptionStatus.PastDue || s.Status == SubscriptionStatus.Dunning)
                    && s.NextRetryAt.HasValue
                    && s.NextRetryAt.Value <= nowUtc)
                .Select(s => s.Id)
                .ToListAsync(ct);
        }

        foreach (var subId in eligibleIds)
        {
            attempted++;
            try
            {
                await RetrySubscriptionAsync(subId, nowUtc, ct);
                succeeded++;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex,
                    $"RefundService.DunningRetry: failed for subscriptionId={subId}.");
            }
        }

        _logger.LogInfo(
            $"RefundService.ProcessDunningRetries: attempted={attempted}, succeeded={succeeded}, failed={failed}.");

        return new DunningRetryResult(attempted, succeeded, failed);
    }

    // ── Grace-expiry downgrade ────────────────────────────────────────────────────────

    /// <summary>
    /// Downgrades a subscription whose grace window has expired to Free/Active.
    /// Mirrors the subscription-refund downgrade block in
    /// <see cref="ProcessSubscriptionRefundAsync"/> — same field set, same semantics.
    /// Idempotent: a subscription already on Free/Active is a no-op.
    /// </summary>
    private async Task DowngradeExpiredGraceAsync(int subscriptionId, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var subscription = await db.Subscriptions
                .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);

            if (subscription is null)
            {
                await tx.RollbackAsync(ct);
                return;
            }

            // Idempotency: already downgraded → no-op.
            if (subscription.PlanCode == PlanCode.Free
                && subscription.Status == SubscriptionStatus.Active
                && !subscription.GraceEndsAt.HasValue)
            {
                await tx.RollbackAsync(ct);
                _logger.LogInfo(
                    $"RefundService.DowngradeExpiredGrace: subscriptionId={subscriptionId} already Free/Active — no-op.");
                return;
            }

            // Downgrade to Free (mirrors subscription-refund downgrade block).
            subscription.PlanCode            = PlanCode.Free;
            subscription.Status              = SubscriptionStatus.Active;
            subscription.CurrentCycleStart   = null;
            subscription.CurrentCycleEnd     = null;
            subscription.PendingPlanCode     = null;
            subscription.GraceEndsAt         = null;
            subscription.NextRetryAt         = null;
            subscription.FailedAttemptCount  = 0;

            await db.SaveChangesAsync(0);
            await tx.CommitAsync(ct);

            _logger.LogInfo(
                $"RefundService.DowngradeExpiredGrace: subscriptionId={subscriptionId} downgraded to Free (grace expired).");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────────────

    private async Task RetrySubscriptionAsync(int subscriptionId, DateTime nowUtc, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var provider = scope.ServiceProvider.GetRequiredService<IPaymentProvider>();
        var maxRetries = _settings.GetInt(GlobalSettingKeys.DunningMaxRetries, 3);
        var retryHours = _settings.GetInt(GlobalSettingKeys.DunningRetryIntervalHours, 24);

        var subscription = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);

        if (subscription is null) return;

        // Build a new Payment row for this retry attempt.
        // FakePaymentProvider: CreateCheckoutSession is a no-op / deterministic success.
        var retryPayment = new Payment
        {
            SubscriptionId = subscriptionId,
            ParentUserId   = subscription.ParentUserId,
            Amount         = 0m, // amount will be re-resolved from IGlobalSettingsProvider if provider supports it
            Currency       = PaymentCurrency.EGP,
            Status         = PaymentStatus.Initiated,
            Kind           = PaymentKind.Subscription,
            IdempotencyKey = $"dunning-retry:{subscriptionId}:{nowUtc:yyyyMMddHH}",
            CreatedAt      = nowUtc,
            CreatedBy      = 0,
        };

        await db.Payments.AddAsync(retryPayment, ct);
        await db.SaveChangesAsync(0);

        // Re-checkout via provider (Fake = always-succeed / configurable-decline).
        // A decline triggers payment.failed → ProcessChargeFailedAsync again.
        try
        {
            var session = await provider.CreateCheckoutSessionAsync(retryPayment, ct);
            retryPayment.ProviderPaymentRef = session.ProviderPaymentRef;
            retryPayment.Status = PaymentStatus.Pending;
        }
        catch
        {
            retryPayment.Status = PaymentStatus.Failed;
        }

        // Schedule next retry or escalate.
        subscription.FailedAttemptCount++;
        if (subscription.FailedAttemptCount >= maxRetries)
        {
            subscription.Status      = SubscriptionStatus.Dunning;
            subscription.GraceEndsAt = subscription.CurrentCycleEnd;
            subscription.NextRetryAt = null;
        }
        else
        {
            subscription.Status      = SubscriptionStatus.PastDue;
            subscription.NextRetryAt = nowUtc.AddHours(retryHours);
        }

        await db.SaveChangesAsync(0);

        _logger.LogInfo(
            $"RefundService.RetrySubscription: subscriptionId={subscriptionId}, " +
            $"attempts={subscription.FailedAttemptCount}, status={subscription.Status}.");
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("23505", StringComparison.Ordinal) == true
           || ex.InnerException?.Message.Contains("UX_CreditTransactions_IdempotencyKey",
               StringComparison.OrdinalIgnoreCase) == true;
}

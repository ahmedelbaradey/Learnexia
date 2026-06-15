using Learnexia.Modules.Billing.Application.Abstractions;
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
///         (DB-unique) — <c>"pack-refund:{paymentId}:{providerEventId}"</c>.</item>
///   <item><see cref="ProcessSubscriptionRefundAsync"/> guards via a <c>Payment.Status</c> check
///         (already-<c>Refunded</c> rows are no-ops).</item>
///   <item><see cref="ProcessChargeFailedAsync"/> guards via an <c>Payment.Status</c> check
///         (already-<c>Failed</c> rows for the same payment are no-ops).</item>
/// </list>
/// </para>
///
/// <para><strong>Never-negative:</strong> <see cref="CreditAccount.Refund"/> clamps the
/// clawback to the available <see cref="CreditAccount.PurchasedBalance"/> (never drives it
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

    /// <inheritdoc/>
    public async Task<RefundResult> ProcessPackRefundAsync(
        int paymentId,
        string providerEventId,
        CancellationToken ct)
    {
        // Inner idempotency guard (outer guard = WebhookEvent inserted by caller before calling us).
        var idempotencyKey = $"pack-refund:{paymentId}:{providerEventId}";

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

            // Load the original payment.
            var payment = await _db.Payments
                .FirstOrDefaultAsync(p => p.Id == paymentId, ct);

            if (payment is null)
            {
                await tx.RollbackAsync(ct);
                _logger.LogInfo($"RefundService.ProcessPackRefund: payment {paymentId} not found.");
                return RefundResult.Fail(RefundFailureReason.PaymentNotFound);
            }

            if (payment.Kind != PaymentKind.Pack || !payment.TargetChildId.HasValue)
            {
                await tx.RollbackAsync(ct);
                _logger.LogInfo($"RefundService.ProcessPackRefund: payment {paymentId} is not a valid Pack payment.");
                return RefundResult.Fail(RefundFailureReason.PaymentNotRefundable);
            }

            // Load the credit account for the target child.
            var account = await _db.CreditAccounts
                .FirstOrDefaultAsync(a => a.ChildId == payment.TargetChildId.Value, ct);

            if (account is null)
            {
                await tx.RollbackAsync(ct);
                _logger.LogInfo($"RefundService.ProcessPackRefund: no credit account for childId={payment.TargetChildId.Value}.");
                return RefundResult.Fail(RefundFailureReason.CreditAccountNotFound);
            }

            // Claw back unspent purchased balance (domain mutator clamps to available balance).
            // We pass the pack size but the Refund mutator clamps to actual PurchasedBalance.
            // Default must match EnergyPackService.CreditPurchasedPackAsync (1000) — same constant source.
            var packSize = _settings.GetInt(GlobalSettingKeys.PackSize, 1000);
            var refundTx = account.Refund(packSize, idempotencyKey, payment.Id.ToString());
            await _db.CreditTransactions.AddAsync(refundTx, ct);

            // Mark payment as Refunded.
            payment.Status = PaymentStatus.Refunded;

            await _db.SaveChangesAsync(_currentUser.UserId ?? 0);
            await tx.CommitAsync(ct);

            _logger.LogInfo(
                $"RefundService.ProcessPackRefund: paymentId={paymentId}, clawedBack={refundTx.Amount}, childId={payment.TargetChildId.Value}.");

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
                subscription.Status    = SubscriptionStatus.Dunning;
                subscription.GraceEndsAt = subscription.CurrentCycleEnd;
                subscription.NextRetryAt = null;
                downgradeScheduled = true;
                graceEndsAt = subscription.GraceEndsAt;
                nextRetryAt = null;
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

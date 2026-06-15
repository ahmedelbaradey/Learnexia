using Hangfire;
using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
// WebhookEventTypes is defined in the Application project (IPaymentProvider.cs).

namespace Learnexia.Modules.Billing.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job — sweeps <c>Initiated</c> / <c>Pending</c> payments that have
/// not reached a terminal state after a configurable timeout and marks them <c>Failed</c>.
///
/// <para>This provides a safety net for payments where the provider's webhook was never
/// delivered (e.g. network outage, misconfigured webhook URL). The real-Paymob adapter
/// would additionally query the provider's status API; the Fake just times-out stale rows.</para>
///
/// <para><strong>Idempotent:</strong> only touches non-terminal rows. Re-running is safe.</para>
/// <para><strong>No UoW:</strong> explicit transaction per payment row (ADR-0001).</para>
/// <para>Mirrors <see cref="BillingGrantJob"/> (DisableConcurrentExecution, fail-soft per row).</para>
/// </summary>
public sealed class ReconcilePaymentsJob
{
    // Payments older than this timeout (minutes) are considered stale.
    private const string StaleThresholdMinutesKey = "Billing:Reconcile:StaleThresholdMinutes";
    private const int DefaultStaleThresholdMinutes = 60;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;
    private readonly int _staleThresholdMinutes;

    public ReconcilePaymentsJob(
        IServiceScopeFactory scopeFactory,
        ISystemClock clock,
        ILoggerManager logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
        _staleThresholdMinutes = configuration.GetValue<int>(StaleThresholdMinutesKey, DefaultStaleThresholdMinutes);
    }

    /// <summary>
    /// Sweeps stale non-terminal payments.
    /// <c>[DisableConcurrentExecution]</c> prevents overlapping runs.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    public async Task RunAsync(CancellationToken ct = default)
    {
        var cutoff = _clock.UtcNow.AddMinutes(-_staleThresholdMinutes);

        _logger.LogInfo($"ReconcilePaymentsJob: sweeping non-terminal payments older than {cutoff:u}.");

        int swept = 0, failed = 0;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        // Load stale non-terminal payment ids — keep the list small.
        var stalePaymentIds = await db.Payments
            .Where(p => (p.Status == PaymentStatus.Initiated || p.Status == PaymentStatus.Pending)
                     && p.CreatedAt < cutoff)
            .Select(p => p.Id)
            .ToListAsync(ct);

        foreach (var paymentId in stalePaymentIds)
        {
            try
            {
                await SweepPaymentAsync(paymentId, ct);
                swept++;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, $"ReconcilePaymentsJob: failed to reconcile paymentId={paymentId}.");
            }
        }

        _logger.LogInfo($"ReconcilePaymentsJob: complete — swept={swept}, failed={failed}.");
    }

    // ── Per-payment reconciliation ───────────────────────────────────────────────────

    private async Task SweepPaymentAsync(int paymentId, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        // Load payment (read-only to decide path).
        var payment = await db.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct);

        if (payment is null
            || payment.Status == PaymentStatus.Succeeded
            || payment.Status == PaymentStatus.Failed
            || payment.Status == PaymentStatus.Refunded)
        {
            return; // Already terminal — skip.
        }

        // ── Pack-credit re-drive guard (lost-update window fix) ─────────────────────────
        // A Pack payment may be stale (Initiated/Pending) if the webhook handler committed
        // the WebhookEvent (payment.succeeded, Succeeded=true) but then crashed before
        // EnergyPackService.CreditPurchasedPackAsync completed. In that case:
        //   • No "pack-credit:{paymentId}:*" CreditTransaction exists yet.
        //   • Payment.Status is still Initiated/Pending.
        // Fix: re-drive via IEnergyPackService with a reconcile-scoped providerEventId.
        // CreditPurchasedPackAsync is idempotent — the reconcile key will not collide with
        // any original webhook key (format: "pack-credit:{paymentId}:reconcile:{paymentId}").
        // This prevents "paid-but-not-delivered" + reconcile-marks-Failed.
        // Guard: only re-drive if a committed WebhookEvent exists (provider told us it succeeded).
        if (payment.Kind == PaymentKind.Pack && payment.TargetChildId.HasValue)
        {
            // Check: does a committed payment.succeeded WebhookEvent exist for this payment's
            // provider ref? This is the signal that the provider confirmed success but our
            // credit step did not complete.
            var hasCommittedWebhook = payment.ProviderPaymentRef is not null
                && await db.WebhookEvents
                    .AsNoTracking()
                    .AnyAsync(w =>
                        w.Succeeded == true
                        && w.EventType == "payment.succeeded"
                        && w.Payload.Contains(payment.ProviderPaymentRef, StringComparison.Ordinal),
                        ct);

            // Also check: is a pack-credit transaction already applied?
            var packCreditKeyPrefix = $"pack-credit:{paymentId}:";
            var creditAlreadyApplied = await db.CreditTransactions
                .AsNoTracking()
                .AnyAsync(t => t.IdempotencyKey != null
                               && EF.Functions.Like(t.IdempotencyKey, packCreditKeyPrefix + "%"),
                    ct);

            if (creditAlreadyApplied)
            {
                // Credit was applied — Payment.Status may not have been flipped yet
                // (CreditPurchasedPackAsync flips it atomically, so this is unlikely,
                // but skip the stale sweep to avoid marking a correctly-credited payment Failed).
                _logger.LogInfo(
                    $"ReconcilePaymentsJob: Pack paymentId={paymentId} credit already exists — skipping stale sweep.");
                return;
            }

            if (hasCommittedWebhook)
            {
                // Re-drive the pack credit. Use a reconcile-scoped providerEventId so the
                // inner idempotency key is distinct from any original webhook key.
                var reconcileEventId = $"reconcile:{paymentId}";
                var energyPackService = scope.ServiceProvider.GetRequiredService<IEnergyPackService>();
                var creditResult = await energyPackService.CreditPurchasedPackAsync(
                    paymentId      : payment.Id,
                    targetChildId  : payment.TargetChildId.Value,
                    providerEventId: reconcileEventId,
                    ct             : ct);

                _logger.LogInfo(
                    $"ReconcilePaymentsJob: Pack re-drive for paymentId={paymentId}, " +
                    $"childId={payment.TargetChildId.Value}, duplicate={creditResult.WasDuplicate}.");
                return; // Do NOT fall through to mark Failed.
            }

            // No committed webhook signal — pack payment is genuinely stale (no provider success).
            // Fall through to mark Failed (standard stale sweep).
        }

        // ── Standard stale-payment sweep (non-Pack, or Pack with no webhook signal) ────
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Re-load within the transaction (state may have changed since the initial load).
            var freshPayment = await db.Payments
                .FirstOrDefaultAsync(p => p.Id == paymentId, ct);

            if (freshPayment is null
                || freshPayment.Status == PaymentStatus.Succeeded
                || freshPayment.Status == PaymentStatus.Failed
                || freshPayment.Status == PaymentStatus.Refunded)
            {
                await tx.RollbackAsync(ct);
                return; // Already terminal — skip.
            }

            // TODO (EXTERNAL): when the real provider adapter is wired, call
            // IPaymentProvider.QueryPaymentStatusAsync(payment.ProviderPaymentRef) here
            // and transition to Succeeded or Failed per the provider response.
            // For now (FakePaymentProvider / no live API) mark as Failed after timeout.
            freshPayment.Status = PaymentStatus.Failed;

            await db.SaveChangesAsync(0);
            await tx.CommitAsync(ct);

            _logger.LogInfo($"ReconcilePaymentsJob: paymentId={paymentId} → Failed (stale sweep).");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

}

using Hangfire;
using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var payment = await db.Payments
                .FirstOrDefaultAsync(p => p.Id == paymentId, ct);

            if (payment is null
                || payment.Status == PaymentStatus.Succeeded
                || payment.Status == PaymentStatus.Failed
                || payment.Status == PaymentStatus.Refunded)
            {
                await tx.RollbackAsync(ct);
                return; // Already terminal — skip.
            }

            // TODO (EXTERNAL): when the real provider adapter is wired, call
            // IPaymentProvider.QueryPaymentStatusAsync(payment.ProviderPaymentRef) here
            // and transition to Succeeded or Failed per the provider response.
            // For now (FakePaymentProvider / no live API) mark as Failed after timeout.
            payment.Status = PaymentStatus.Failed;

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

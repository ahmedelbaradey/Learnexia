using Hangfire;
using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Billing.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job — processes <c>PastDue</c> / <c>Dunning</c> subscriptions
/// whose <c>NextRetryAt</c> has passed, re-attempting the charge via
/// <see cref="IPaymentProvider"/> and scheduling further retries or triggering final downgrade.
///
/// <para><strong>Design:</strong> Mirrors <see cref="BillingGrantJob"/>:
/// <list type="bullet">
///   <item><c>[DisableConcurrentExecution]</c> prevents two instances from overlapping.</item>
///   <item>All state-mutation logic lives in <see cref="IRefundService.ProcessDunningRetriesAsync"/>
///         — this job stays THIN (resolve clock → call service → log summary).</item>
///   <item>Retry policy and intervals are config-driven via <see cref="GlobalSettingKeys.DunningMaxRetries"/>
///         / <see cref="GlobalSettingKeys.DunningRetryIntervalHours"/>.</item>
///   <item>Fail-soft per subscription: errors are caught inside
///         <see cref="IRefundService.ProcessDunningRetriesAsync"/>; this job never aborts the
///         sweep on a single subscription failure.</item>
/// </list>
/// </para>
///
/// <para><strong>Idempotency:</strong> re-running for the same <c>nowUtc</c> (e.g. Hangfire
/// retry after a transient error) is safe — the per-subscription idempotency key
/// <c>"dunning-retry:{subscriptionId}:{yyyyMMddHH}"</c> prevents double-charging within the
/// same hour, and the status re-check inside <see cref="IRefundService.ProcessChargeFailedAsync"/>
/// ignores already-<c>Failed</c> payments.</para>
///
/// <para>Registered in <see cref="BillingModule.InitializeAsync"/> via
/// <c>RecurringJobManager.AddOrUpdate</c>.</para>
/// </summary>
public sealed class DunningRetryJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;

    public DunningRetryJob(
        IServiceScopeFactory scopeFactory,
        ISystemClock clock,
        ILoggerManager logger)
    {
        _scopeFactory = scopeFactory;
        _clock        = clock;
        _logger       = logger;
    }

    /// <summary>
    /// Executes the dunning retry sweep.
    /// <c>[DisableConcurrentExecution]</c> prevents two instances from overlapping.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task RunAsync(CancellationToken ct = default)
    {
        var nowUtc = _clock.UtcNow;
        _logger.LogInfo($"DunningRetryJob: starting sweep at nowUtc={nowUtc:O}.");

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var refundService = scope.ServiceProvider.GetRequiredService<IRefundService>();

            var result = await refundService.ProcessDunningRetriesAsync(nowUtc, ct);

            _logger.LogInfo(
                $"DunningRetryJob: complete — attempted={result.Attempted}, " +
                $"succeeded={result.Succeeded}, failed={result.Failed}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DunningRetryJob: unexpected error in sweep.");
            throw; // Re-throw so Hangfire records the failure and retries according to its own policy.
        }
    }
}

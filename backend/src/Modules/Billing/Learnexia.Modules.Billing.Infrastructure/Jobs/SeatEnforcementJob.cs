using Hangfire;
using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Billing.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job — applies seat enforcement across all subscriptions that have
/// expired grace windows OR cycle-end removal markers (P10-15-BE-4).
///
/// <para><strong>Design:</strong> mirrors <see cref="DunningRetryJob"/>:
/// <list type="bullet">
///   <item><c>[DisableConcurrentExecution]</c> prevents two instances from overlapping.</item>
///   <item>All per-subscription logic lives in <see cref="ISeatEnforcementService.EnforceAsync"/>
///         — this job stays thin (query candidates → call service → log summary).</item>
///   <item>Fail-soft per subscription: errors for one subscription are caught and logged; the
///         sweep continues for the rest.</item>
/// </list>
/// </para>
///
/// <para><strong>Candidates:</strong>
/// <list type="bullet">
///   <item>Subscriptions with <c>GraceEndsAt &lt;= nowUtc</c> (grace expired → enforce over-limit seats).</item>
///   <item>Subscriptions with any <c>SeatReservation.RemovalScheduledAt &lt;= nowUtc</c>
///         (cycle-end removal markers due).</item>
/// </list>
/// </para>
///
/// <para>Registered in <see cref="BillingModule.InitializeAsync"/> via
/// <c>RecurringJobManager.AddOrUpdate</c>. Runs daily (configurable).</para>
/// </summary>
public sealed class SeatEnforcementJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;

    public SeatEnforcementJob(
        IServiceScopeFactory scopeFactory,
        ISystemClock clock,
        ILoggerManager logger)
    {
        _scopeFactory = scopeFactory;
        _clock        = clock;
        _logger       = logger;
    }

    /// <summary>
    /// Executes the seat enforcement sweep.
    /// <c>[DisableConcurrentExecution]</c> prevents two instances from overlapping.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task RunAsync(CancellationToken ct = default)
    {
        var nowUtc = _clock.UtcNow;
        _logger.LogInfo($"SeatEnforcementJob: starting sweep at nowUtc={nowUtc:O}.");

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var enforcementService = scope.ServiceProvider.GetRequiredService<ISeatEnforcementService>();

            // Find subscription ids needing enforcement:
            // (a) GraceEndsAt expired (grace window closed — enforce over-limit seats), OR
            // (b) HasReservationsWithScheduledRemovalsDue (cycle-end removal markers passed).
            var graceExpiredIds = await db.Subscriptions
                .AsNoTracking()
                .Where(s => s.GraceEndsAt.HasValue && s.GraceEndsAt <= nowUtc
                         && (s.Status == SubscriptionStatus.Dunning || s.Status == SubscriptionStatus.Active))
                .Select(s => s.Id)
                .ToListAsync(ct);

            var scheduledRemovalIds = await db.SeatReservations
                .AsNoTracking()
                .Where(r => r.RemovalScheduledAt.HasValue
                         && r.RemovalScheduledAt <= nowUtc
                         && (r.Status == SeatStatus.Active || r.Status == SeatStatus.Reserved))
                .Select(r => r.SubscriptionId)
                .Distinct()
                .ToListAsync(ct);

            var candidateIds = graceExpiredIds
                .Union(scheduledRemovalIds)
                .Distinct()
                .ToList();

            if (candidateIds.Count == 0)
            {
                _logger.LogInfo("SeatEnforcementJob: no candidates — sweep complete.");
                return;
            }

            var totalLocked = 0;
            var errors = 0;

            foreach (var subscriptionId in candidateIds)
            {
                try
                {
                    // Create a fresh scope per subscription so the DbContext is not shared across
                    // concurrent enforcement passes — mirrors the DunningRetryJob pattern.
                    await using var subScope = _scopeFactory.CreateAsyncScope();
                    var subEnforcement = subScope.ServiceProvider.GetRequiredService<ISeatEnforcementService>();
                    var locked = await subEnforcement.EnforceAsync(subscriptionId, ct);
                    totalLocked += locked;
                }
                catch (Exception ex)
                {
                    errors++;
                    _logger.LogError(ex, $"SeatEnforcementJob: error enforcing subscriptionId={subscriptionId}.");
                }
            }

            _logger.LogInfo(
                $"SeatEnforcementJob: complete — candidates={candidateIds.Count}, " +
                $"totalLocked={totalLocked}, errors={errors}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SeatEnforcementJob: unexpected error in sweep.");
            throw;
        }
    }
}

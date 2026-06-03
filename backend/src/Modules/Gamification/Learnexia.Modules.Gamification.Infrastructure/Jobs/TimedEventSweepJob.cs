using Hangfire;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Gamification.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job — runs every 2 minutes (<c>"*/2 * * * *"</c>, UTC) — P4-11-B1-B.
///
/// Purpose: transitions <see cref="Domain.Entities.TimedEvent"/> rows between active/inactive
/// state as their <c>StartUtc</c> / <c>EndUtc</c> windows pass. Uses <c>IsActive</c> as an
/// idempotency latch — each transition fires its domain event exactly once per state flip.
///
/// Algorithm (two-pass, idempotent):
/// <list type="number">
///   <item>Pass 1 (activation): load rows where <c>StartUtc &lt;= now AND EndUtc &gt; now AND !IsActive</c>.
///         For each: call <c>Activate()</c> → save. Then publish <see cref="TimedEventStartedDomainEvent"/>.</item>
///   <item>Pass 2 (deactivation): load rows where <c>EndUtc &lt;= now AND IsActive</c>.
///         For each: call <c>Deactivate()</c> → save. Then publish <see cref="TimedEventEndedDomainEvent"/>.</item>
/// </list>
///
/// Domain events are published via <see cref="IPublisher"/> AFTER the per-row save (post-commit pattern),
/// so cache invalidators and republishers always see committed state. Each publish is individually
/// try/caught — a publish failure does NOT roll back the already-committed state flip (fail-soft
/// per ADR 0002 §3).
///
/// Idempotent: running twice in the same minute → second pass finds 0 rows (all <c>IsActive</c>
/// flags already in the correct state) → 0 events published.
///
/// Hangfire resolves this class via DI — registered as Transient in
/// <c>AddGamificationInfrastructure</c>; the recurring job is wired in
/// <c>GamificationModule.InitializeAsync</c>.
/// </summary>
public sealed class TimedEventSweepJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerManager _logger;
    private readonly ISystemClock _clock;

    public TimedEventSweepJob(
        IServiceScopeFactory scopeFactory,
        ILoggerManager logger,
        ISystemClock clock)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
        _clock        = clock;
    }

    /// <summary>
    /// Executes the sweep. Creates a fresh DI scope with its own <see cref="GamificationDbContext"/>
    /// and <see cref="IPublisher"/> (Hangfire worker has no HTTP request scope — a new scope is mandatory).
    ///
    /// <c>[DisableConcurrentExecution]</c> prevents two instances of this 2-minute job from
    /// overlapping if a run takes longer than the cron interval — fixes P4-11 audit Medium #4.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    public async Task RunAsync(CancellationToken ct)
    {
        var nowUtc = _clock.UtcNow;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db        = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        int activatedCount   = 0;
        int deactivatedCount = 0;
        int publishFailCount = 0;

        // ── Pass 1: Activate events whose window has opened ──────────────────────────────────
        var toActivate = await db.TimedEvents
            .Where(e => !e.IsActive && e.StartUtc <= nowUtc && e.EndUtc > nowUtc)
            .ToListAsync(ct);

        foreach (var ev in toActivate)
        {
            ev.Activate();
        }

        if (toActivate.Count > 0)
        {
            await db.SaveChangesAsync(0);
            activatedCount = toActivate.Count;
        }

        // Publish started events AFTER the save — post-commit guarantees committed state.
        foreach (var ev in toActivate)
        {
            try
            {
                await publisher.Publish(new TimedEventStartedDomainEvent(
                    TimedEventId: ev.Id,
                    Code:         ev.Code,
                    Multiplier:   ev.Multiplier,
                    Scope:        ev.Scope,
                    StartUtc:     ev.StartUtc,
                    EndUtc:       ev.EndUtc,
                    OccurredAtUtc: nowUtc), ct);
            }
            catch (Exception ex)
            {
                publishFailCount++;
                _logger.LogError(ex,
                    $"P4-11: TimedEventSweepJob — started publish failed for code={ev.Code}.");
            }
        }

        // ── Pass 2: Deactivate events whose window has closed ────────────────────────────────
        var toDeactivate = await db.TimedEvents
            .Where(e => e.IsActive && e.EndUtc <= nowUtc)
            .ToListAsync(ct);

        foreach (var ev in toDeactivate)
        {
            ev.Deactivate();
        }

        if (toDeactivate.Count > 0)
        {
            await db.SaveChangesAsync(0);
            deactivatedCount = toDeactivate.Count;
        }

        // Publish ended events AFTER the save.
        foreach (var ev in toDeactivate)
        {
            try
            {
                await publisher.Publish(new TimedEventEndedDomainEvent(
                    TimedEventId: ev.Id,
                    Code:         ev.Code,
                    OccurredAtUtc: nowUtc), ct);
            }
            catch (Exception ex)
            {
                publishFailCount++;
                _logger.LogError(ex,
                    $"P4-11: TimedEventSweepJob — ended publish failed for code={ev.Code}.");
            }
        }

        _logger.LogInfo(
            $"P4-11: TimedEventSweepJob — activated={activatedCount}, deactivated={deactivatedCount}, " +
            $"publishFailed={publishFailCount}, nowUtc={nowUtc:O}.");
    }
}

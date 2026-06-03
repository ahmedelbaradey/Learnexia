using Hangfire;
using Learnexia.Modules.Gamification.Application.Configuration;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job — runs at 00:05 UTC daily (<c>"5 0 * * *"</c>).
///
/// Purpose (AC4, D4): defensive observability sweep. The lazy break-detection in
/// <c>AdvanceStreakCommandHandler</c> is the source of truth for streak state; this job covers
/// students who go silent and would otherwise keep a stale non-zero <c>CurrentStreak</c> on the
/// dashboard indefinitely.
///
/// Algorithm (P4-11 two-pass with freeze branch):
/// <list type="number">
///   <item>
///     <b>Pass 1 (bulk — performance):</b> capture student IDs + streaks for profiles where
///     <c>CurrentStreak &gt; 0 AND LastActivityDateUtc &lt; threshold AND FreezeBalance = 0</c>.
///     Execute a single <c>ExecuteUpdateAsync</c> to set <c>CurrentStreak = 0</c> for that
///     filtered set. Publish <see cref="StreakBrokenIntegrationEvent"/> per captured ID
///     (capture-before-bulk pattern). This is the pre-P4-11 path, narrowed by the
///     <c>FreezeBalance = 0</c> predicate.
///   </item>
///   <item>
///     <b>Pass 2 (per-row — freeze consumers):</b> load profiles where
///     <c>CurrentStreak &gt; 0 AND LastActivityDateUtc &lt; threshold AND FreezeBalance &gt; 0</c>
///     (change-tracked). For each, call
///     <see cref="StudentXpProfile.ConsumeFreezeAndShiftActivity"/> which decrements
///     <c>FreezeBalance</c>, shifts <c>LastActivityDateUtc</c> to <c>threshold</c> (yesterday),
///     and raises <see cref="Domain.Events.StreakFreezeConsumedDomainEvent"/> on the entity.
///     After saving, collect and dispatch the domain event via <see cref="IPublisher"/>.
///     <c>CurrentStreak</c> is NOT reset. No <see cref="StreakBrokenIntegrationEvent"/> is raised.
///   </item>
/// </list>
///
/// Idempotent: running twice consecutively — Pass 1 finds 0 rows (already reset),
/// Pass 2 finds 0 rows (FreezeBalance already decremented / LastActivityDateUtc already shifted).
///
/// TOCTOU note: rows captured between the SELECT and the ExecuteUpdateAsync that a concurrent write
/// updates to a new streak may receive a false-positive integration event. This is bounded, low-
/// likelihood, and acceptable for MVP (P4-09 brief R7).
///
/// Hangfire resolves this class via DI — registered as Transient in
/// <c>AddGamificationInfrastructure</c>; the recurring job is wired in
/// <c>GamificationModule.InitializeAsync</c>.
/// </summary>
public sealed class StreakSweepJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerManager _logger;
    private readonly ISystemClock _clock;
    private readonly IOptions<StreakOptions> _options;

    public StreakSweepJob(
        IServiceScopeFactory scopeFactory,
        ILoggerManager logger,
        ISystemClock clock,
        IOptions<StreakOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _clock = clock;
        _options = options;
    }

    /// <summary>
    /// Executes the sweep. Creates a fresh DI scope with its own <see cref="GamificationDbContext"/>
    /// and <see cref="IPublisher"/> (Hangfire worker has no HTTP request scope — a new scope is mandatory).
    ///
    /// <c>[DisableConcurrentExecution]</c> prevents two instances of this daily job from
    /// overlapping if a run is delayed or retried — fixes P4-11 audit Medium #4.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    public async Task RunAsync(CancellationToken ct)
    {
        var nowUtc = _clock.UtcNow;
        var today = StreakDayCalculator.Today(_options.Value.TimeZoneId, _clock);
        var threshold = today.AddDays(-1);   // anything older than yesterday = broken

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        // ── Pass 1: Bulk-reset profiles with FreezeBalance == 0 ────────────────────────────
        // Capture affected rows BEFORE the bulk update (lightweight projection).
        // Narrowed by FreezeBalance = 0 — freeze-holders are handled in Pass 2.
        var brokenCandidates = await db.StudentXpProfiles
            .AsNoTracking()
            .Where(p => p.CurrentStreak > 0
                     && (p.LastActivityDateUtc == null || p.LastActivityDateUtc < threshold)
                     && p.FreezeBalance == 0)
            .Select(p => new { p.StudentId, p.CurrentStreak })
            .ToListAsync(ct);

        // Bulk UPDATE — EF 7+ ExecuteUpdateAsync issues a single SQL statement.
        // Does NOT go through the change tracker → no domain events from this path.
        var rowsReset = await db.StudentXpProfiles
            .Where(p => p.CurrentStreak > 0
                     && (p.LastActivityDateUtc == null || p.LastActivityDateUtc < threshold)
                     && p.FreezeBalance == 0)
            .ExecuteUpdateAsync(
                s => s.SetProperty(p => p.CurrentStreak, 0),
                ct);

        // Publish one StreakBrokenIntegrationEvent per broken student.
        // Each publish is individually try/caught — a publish failure does NOT roll back
        // the already-committed bulk update (fail-soft per ADR 0002 §3).
        int brokenPublishedCount = 0;
        int brokenPublishFailedCount = 0;

        foreach (var row in brokenCandidates)
        {
            try
            {
                await publisher.Publish(new StreakBrokenIntegrationEvent(
                    EventId: Guid.NewGuid(),
                    OccurredOnUtc: nowUtc,
                    StudentId: row.StudentId,
                    PreviousStreak: row.CurrentStreak,
                    BrokenAtDate: threshold), ct);

                brokenPublishedCount++;
            }
            catch (Exception ex)
            {
                brokenPublishFailedCount++;
                _logger.LogError(ex,
                    $"P4-09: StreakSweepJob — break publish failed for studentId={row.StudentId}.");
            }
        }

        // ── Pass 2: Per-row freeze consumption ─────────────────────────────────────────────
        // Load change-tracked profiles whose streak would break but have a freeze to spend.
        // Batched in pages of 500 to bound memory; outer loop continues while rows remain.
        int freezeConsumedCount = 0;
        int freezePublishFailedCount = 0;

        List<StudentXpProfile> freezeCandidates;
        do
        {
            freezeCandidates = await db.StudentXpProfiles
                .Where(p => p.CurrentStreak > 0
                         && (p.LastActivityDateUtc == null || p.LastActivityDateUtc < threshold)
                         && p.FreezeBalance > 0)
                .Take(500)
                .ToListAsync(ct);

            foreach (var profile in freezeCandidates)
            {
                try
                {
                    // Consume 1 freeze, shift LastActivityDateUtc to threshold (yesterday),
                    // and raise StreakFreezeConsumedDomainEvent on the entity.
                    // CurrentStreak is NOT changed — the streak survives.
                    profile.ConsumeFreezeAndShiftActivity(threshold, nowUtc);

                    // Commit this row's changes.
                    await db.SaveChangesAsync(0);

                    // Count the successful freeze consumption per profile — outside the inner
                    // domain-event dispatch loop so the counter reflects actual profile saves,
                    // not the number of events published (fixes P4-11 audit Low #6).
                    freezeConsumedCount++;

                    // Collect domain events from the change tracker and dispatch post-commit.
                    var aggregates = db.ChangeTracker
                        .Entries<AggregateRoot>()
                        .Where(e => e.Entity.DomainEvents.Count > 0)
                        .Select(e => e.Entity)
                        .ToList();

                    foreach (var aggregate in aggregates)
                    {
                        var domainEvents = aggregate.DomainEvents.ToList();
                        aggregate.ClearDomainEvents();

                        foreach (var domainEvent in domainEvents)
                        {
                            try
                            {
                                await publisher.Publish(domainEvent, ct);
                            }
                            catch (Exception ex)
                            {
                                freezePublishFailedCount++;
                                _logger.LogError(ex,
                                    $"P4-11: StreakSweepJob — freeze-consumed publish failed " +
                                    $"for studentId={profile.StudentId}.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        $"P4-11: StreakSweepJob — freeze consumption failed " +
                        $"for studentId={profile.StudentId}.");
                }
            }
        }
        while (freezeCandidates.Count == 500);

        _logger.LogInfo(
            $"P4-03/P4-11: streak-sweep complete — " +
            $"brokenCaptured={brokenCandidates.Count}, rowsReset={rowsReset}, " +
            $"brokenPublished={brokenPublishedCount}, brokenPublishFailed={brokenPublishFailedCount}, " +
            $"freezeConsumed={freezeConsumedCount}, freezePublishFailed={freezePublishFailedCount}, " +
            $"threshold={threshold:yyyy-MM-dd}.");
    }
}

using Learnexia.Modules.Gamification.Application.Configuration;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
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
/// Algorithm (P4-09 D6 — capture-before-bulk):
/// <list type="number">
///   <item>Project affected rows (StudentId + CurrentStreak) BEFORE the bulk update.</item>
///   <item>Bulk <c>ExecuteUpdateAsync</c> — sets <c>CurrentStreak = 0</c> for all stale profiles.
///         Does NOT touch <c>LongestStreak</c> or <c>LastActivityDateUtc</c>.</item>
///   <item>Loop captured rows and publish one <see cref="StreakBrokenIntegrationEvent"/> per student
///         (each in its own try/catch — a publish failure does NOT roll back the bulk update).</item>
/// </list>
///
/// This path emits <see cref="StreakBrokenIntegrationEvent"/> directly (no domain object) because
/// <c>ExecuteUpdateAsync</c> bypasses the change tracker. The domain mutation path
/// (<c>StudentXpProfile.ResetStreakAndStart</c>) raises <c>StreakBrokenDomainEvent</c> which is
/// re-published by <c>StreakBrokenDomainEventRepublisher</c>. Both paths produce the same
/// integration event; Notifications-side Redis dedupe prevents duplicate nudges.
///
/// Idempotent: running twice consecutively → second pass touches 0 rows (all already = 0), and
/// the captured list is empty so no integration events are published.
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
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        var today = StreakDayCalculator.Today(_options.Value.TimeZoneId, _clock);
        var threshold = today.AddDays(-1);   // anything older than yesterday = broken

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        // Step 1: Capture affected rows BEFORE the bulk update (lightweight projection).
        // Same WHERE clause as the bulk update below to minimise the TOCTOU window.
        var affected = await db.StudentXpProfiles
            .AsNoTracking()
            .Where(p => p.CurrentStreak > 0
                     && (p.LastActivityDateUtc == null || p.LastActivityDateUtc < threshold))
            .Select(p => new { p.StudentId, p.CurrentStreak })
            .ToListAsync(ct);

        // Step 2: Bulk UPDATE — EF 7+ ExecuteUpdateAsync issues a single SQL statement.
        // Does NOT go through the change tracker → no domain events from this path.
        var rowsAffected = await db.StudentXpProfiles
            .Where(p => p.CurrentStreak > 0
                     && (p.LastActivityDateUtc == null || p.LastActivityDateUtc < threshold))
            .ExecuteUpdateAsync(
                s => s.SetProperty(p => p.CurrentStreak, 0),
                ct);

        // Step 3: Publish one StreakBrokenIntegrationEvent per affected student.
        // Each publish is individually try/caught — a publish failure does NOT roll back
        // the already-committed bulk update (fail-soft per ADR 0002 §3).
        int publishedCount = 0;
        int publishFailedCount = 0;

        foreach (var row in affected)
        {
            try
            {
                await publisher.Publish(new StreakBrokenIntegrationEvent(
                    EventId: Guid.NewGuid(),
                    OccurredOnUtc: _clock.UtcNow,
                    StudentId: row.StudentId,
                    PreviousStreak: row.CurrentStreak,
                    BrokenAtDate: threshold), ct);

                publishedCount++;
            }
            catch (Exception ex)
            {
                publishFailedCount++;
                _logger.LogError(ex,
                    $"P4-09: StreakSweepJob — publish failed for studentId={row.StudentId}.");
            }
        }

        _logger.LogInfo(
            $"P4-03/P4-09: streak-sweep complete — " +
            $"capturedAffected={affected.Count}, rowsReset={rowsAffected}, " +
            $"published={publishedCount}, failedPublish={publishFailedCount}, " +
            $"threshold={threshold:yyyy-MM-dd}.");
    }
}

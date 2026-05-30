using Learnexia.Modules.Gamification.Application.Configuration;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
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
/// dashboard indefinitely. Without this job, P4-09 re-engagement nudges can never fire for
/// those students.
///
/// Algorithm: one bulk <c>ExecuteUpdateAsync</c> — sets <c>CurrentStreak = 0</c> for all profiles
/// where <c>CurrentStreak &gt; 0 AND LastActivityDateUtc &lt; today.AddDays(-1)</c>.
/// Does NOT touch <c>LongestStreak</c> or <c>LastActivityDateUtc</c>.
///
/// Idempotent: running twice consecutively → second pass touches 0 rows (all already = 0).
///
/// Note: domain events (<c>StreakBrokenDomainEvent</c>) are NOT raised from the bulk update path
/// (EF <c>ExecuteUpdateAsync</c> bypasses the change tracker). This is intentional for MVP:
/// no handler for <c>StreakBrokenDomainEvent</c> exists in P4-03. When P4-09 is implemented,
/// replace the bulk path with the page-loop below or add a separate domain-event dispatch step.
///
/// Hangfire resolves this class via DI — it is registered as Scoped in
/// <c>AddGamificationInfrastructure</c> and the recurring job is wired in
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
    /// (Hangfire worker has no HTTP request scope — a new scope is mandatory).
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        var today = StreakDayCalculator.Today(_options.Value.TimeZoneId, _clock);
        var threshold = today.AddDays(-1);   // anything older than yesterday = broken

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();

        // Bulk UPDATE — EF 7+ ExecuteUpdateAsync issues a single SQL statement.
        // Does NOT go through the change tracker → no domain events from this path (P4-09 will add them).
        var rowsAffected = await db.StudentXpProfiles
            .Where(p => p.CurrentStreak > 0
                     && (p.LastActivityDateUtc == null || p.LastActivityDateUtc < threshold))
            .ExecuteUpdateAsync(
                s => s.SetProperty(p => p.CurrentStreak, 0),
                ct);

        _logger.LogInfo(
            $"P4-03: streak-sweep complete — rowsReset={rowsAffected}, threshold={threshold:yyyy-MM-dd}.");
    }
}

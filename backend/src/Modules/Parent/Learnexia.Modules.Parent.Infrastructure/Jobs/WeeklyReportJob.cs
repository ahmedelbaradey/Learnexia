using Hangfire;
using Learnexia.Modules.Parent.Application.Abstractions;
using Learnexia.Modules.Parent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Parent.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job — generates the prior-week <c>WeeklyReport</c> for every
/// active (linked) child (P5-01-BE-3).
///
/// <para><strong>Design:</strong> mirrors <c>SeatEnforcementJob</c>:
/// <list type="bullet">
///   <item><c>[DisableConcurrentExecution]</c> prevents two instances from overlapping.</item>
///   <item>All per-child logic lives in <see cref="IWeeklyReportGeneratorService.GenerateAsync"/>
///         — this job stays thin (resolve child ids → call service per child → log summary).</item>
///   <item>Fail-soft per child: an error for one child is caught and logged; the sweep continues
///         for the rest.</item>
///   <item>Fresh DI scope per child: <see cref="IServiceScopeFactory"/> + async scope ensures
///         the <c>ParentDbContext</c> is not shared across calls.</item>
/// </list>
/// </para>
///
/// <para><strong>Prior-week window:</strong> Monday 00:00 UTC of the calendar week that ended
/// before the job fires, computed from <c>DateTime.UtcNow</c> at job start.</para>
///
/// <para><strong>Child enumeration:</strong> resolved directly from the <c>ParentStudents</c>
/// table (Parent module's own persistence — no isolation breach). Returns distinct child ids
/// to avoid duplicate processing when a child is linked to multiple parents.</para>
///
/// <para>Registered in <see cref="Learnexia.Modules.Parent.Api.ParentModule.InitializeAsync"/>
/// via <c>RecurringJobManager.AddOrUpdate</c>. Runs weekly (Mondays 03:00 UTC).</para>
/// </summary>
public sealed class WeeklyReportJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerManager _logger;

    public WeeklyReportJob(IServiceScopeFactory scopeFactory, ILoggerManager logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <summary>
    /// Sweeps all linked children and generates the prior-week <c>WeeklyReport</c> for each.
    /// <c>[DisableConcurrentExecution]</c> prevents two instances from overlapping.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 900)]
    public async Task RunAsync(CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        _logger.LogInfo($"WeeklyReportJob: starting sweep at nowUtc={nowUtc:O}.");

        // ── Prior-week start: last Monday 00:00 UTC ────────────────────────────────────
        // DayOfWeek.Monday = 1; Sunday = 0. The formula maps any day to days-since-Monday,
        // then subtracts 7 to reach the previous Monday (prior week start).
        var daysSinceMonday = ((int)nowUtc.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var currentWeekMonday = nowUtc.Date.AddDays(-daysSinceMonday);
        var priorWeekMonday   = currentWeekMonday.AddDays(-7);

        _logger.LogInfo($"WeeklyReportJob: generating reports for week starting {priorWeekMonday:O}.");

        // ── Resolve all distinct linked child ids ──────────────────────────────────────
        // Parent.Infrastructure owns ParentDbContext — no cross-module isolation issue.
        List<int> childIds;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ParentDbContext>();
            childIds = await db.ParentStudents
                .AsNoTracking()
                .Select(ps => ps.StudentId)
                .Distinct()
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WeeklyReportJob: failed to load child ids — sweep aborted.");
            throw;
        }

        if (childIds.Count == 0)
        {
            _logger.LogInfo("WeeklyReportJob: no linked children found — sweep complete.");
            return;
        }

        _logger.LogInfo($"WeeklyReportJob: {childIds.Count} child(ren) to process.");

        var succeeded = 0;
        var errors    = 0;

        foreach (var childId in childIds)
        {
            try
            {
                // Create a fresh scope per child so the DbContext is not shared across
                // concurrent generator calls — mirrors the SeatEnforcementJob pattern.
                await using var childScope = _scopeFactory.CreateAsyncScope();
                var generator = childScope.ServiceProvider
                    .GetRequiredService<IWeeklyReportGeneratorService>();

                await generator.GenerateAsync(childId, priorWeekMonday, ct);
                succeeded++;
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(ex, $"WeeklyReportJob: error generating report for childId={childId}.");
            }
        }

        _logger.LogInfo(
            $"WeeklyReportJob: complete — total={childIds.Count}, succeeded={succeeded}, errors={errors}.");
    }
}

using Hangfire;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Learning.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job — runs daily at midnight UTC (<c>"0 0 * * *"</c>, configurable).
///
/// Purpose (P3-10, AC3): defensive observability sweep. For each <c>StudentSkillMastery</c> row
/// that is currently in <c>Mastered</c> status with a stale (elapsed) <c>NextReviewDueAt</c>, this
/// job RECOMPUTES <c>NextReviewDueAt = LastPracticedAt + currentIntervalDays</c> from first principles.
///
/// It does NOT advance the ladder (that is BE-6's completion-hook responsibility). It is a pure
/// idempotent recompute: running twice produces the same <c>NextReviewDueAt</c> because the formula
/// inputs (<c>LastPracticedAt</c>, <c>ReviewIntervalDays</c>) are stable between runs.
///
/// Also ensures <c>NeedsReview</c> rows have a <c>NextReviewDueAt</c> set (to <c>LastPracticedAt +
/// NeedsReviewIntervalDays</c>) so the due-list endpoint can surface them consistently.
///
/// Registration: <c>RecurringJob.AddOrUpdate("SR-Sweep", ...)</c> with fixed ID <c>"SR-Sweep"</c>
/// — Hangfire dedupes by this ID across restarts (idempotent registration). Wired in
/// <c>LearningModule.InitializeAsync</c>, mirroring <c>GamificationModule.InitializeAsync</c>.
///
/// Hangfire resolves this class via DI — registered as Transient in
/// <c>AddLearningInfrastructure</c>; the recurring job creates its own inner scope via
/// <see cref="IServiceScopeFactory"/> so it does not participate in any caller's scope.
/// </summary>
public sealed class SpacedRepetitionSweepJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerManager _logger;
    private readonly IOptions<SpacedRepetitionOptions> _options;

    public SpacedRepetitionSweepJob(
        IServiceScopeFactory scopeFactory,
        ILoggerManager logger,
        IOptions<SpacedRepetitionOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
        _options      = options;
    }

    /// <summary>
    /// Executes the sweep. Creates a fresh DI scope with its own <see cref="ILearningRepository"/>
    /// (Hangfire worker has no HTTP request scope — a new scope is mandatory).
    ///
    /// <c>[DisableConcurrentExecution]</c> prevents two instances of this daily job from overlapping
    /// if a run is delayed or retried. Mirrors StreakSweepJob pattern.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    public async Task RunAsync(CancellationToken ct = default)
    {
        var utcNow  = DateTime.UtcNow;
        var options = _options.Value;

        await using var scope      = _scopeFactory.CreateAsyncScope();
        var repository             = scope.ServiceProvider.GetRequiredService<ILearningRepository>();

        // Load all due rows (NeedsReview OR Mastered+stale) — AsNoTracking.
        var dueRows = await repository.GetDueMasteryRowsAsync(utcNow, ct);

        int updatedCount = 0;
        int errorCount   = 0;

        foreach (var row in dueRows)
        {
            try
            {
                int   intervalDays;
                DateTime nextDueAt;

                if (row.Status == MasteryStatus.NeedsReview)
                {
                    // NeedsReview: schedule for immediate surface (today + NeedsReviewIntervalDays).
                    // Pure recompute from LastPracticedAt — idempotent.
                    intervalDays = options.NeedsReviewIntervalDays;
                    nextDueAt    = row.LastPracticedAt.ToUniversalTime()
                                   .AddDays(intervalDays);
                }
                else
                {
                    // Mastered-but-stale: recompute NextReviewDueAt from LastPracticedAt +
                    // current ReviewIntervalDays (no ladder advancement — just idempotent recompute).
                    intervalDays = row.ReviewIntervalDays > 0
                        ? row.ReviewIntervalDays
                        : options.IntervalLadderDays[0];   // first-time: default to ladder[0]

                    nextDueAt = row.LastPracticedAt.ToUniversalTime()
                                .AddDays(intervalDays);
                }

                await repository.UpdateSpacedRepetitionFieldsAsync(
                    row.Id,
                    intervalDays,
                    row.RepetitionNumber,   // ladder position unchanged by sweep
                    nextDueAt,
                    ct);

                updatedCount++;
            }
            catch (Exception ex)
            {
                errorCount++;
                _logger.LogError(ex,
                    $"P3-10: SpacedRepetitionSweepJob — update failed for masteryId={row.Id}, studentId={row.StudentId}.");
            }
        }

        _logger.LogInfo(
            $"P3-10: SR-Sweep complete — dueRows={dueRows.Count}, updated={updatedCount}, errors={errorCount}, utcNow={utcNow:O}.");
    }
}

using Hangfire;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Services;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Learning.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job — runs daily (<c>Cron.Daily()</c>, fixed ID <c>"Rec-Recompute"</c>).
///
/// Purpose (P5-09, AC2): sweep all active students and compute/persist their daily
/// recommendation set. Registered AFTER <c>"SP-Recompute"</c> in
/// <c>LearningModule.InitializeAsync</c> so the behavioral profile (P3-13) is fresh when
/// the recommendation engine runs.
///
/// IDEMPOTENT: calling <c>RecommendationService.ComputeAndUpsertAsync</c> twice for the
/// same (studentId, date) pair is safe — the second call overwrites the first with the
/// same (deterministic) output.
///
/// TRANSACTION NOTE: runs OUTSIDE the HTTP command pipeline — no <c>UnitOfWorkBehavior</c>
/// wrapping the execution. Each call to <c>ComputeAndUpsertAsync</c> stages changes; this
/// job calls <c>LearningDbContext.SaveChangesAsync(userId=0)</c> explicitly to commit the
/// staged changes before moving to the next student (ADR-0001).
///
/// GRADE-TRANSITION PRESERVATION: historical recommendation rows survive a grade change
/// (product rule). The job simply computes fresh rows for the new grade scope.
///
/// Registration: <c>RecurringJob.AddOrUpdate("Rec-Recompute", ...)</c> with fixed ID
/// <c>"Rec-Recompute"</c> — Hangfire dedupes by ID across restarts. Wired in
/// <c>LearningModule.InitializeAsync</c> AFTER <c>"SP-Recompute"</c>.
///
/// Hangfire resolves this class via DI — registered as Transient in
/// <c>AddLearningInfrastructure</c>; creates its own inner scope via
/// <see cref="IServiceScopeFactory"/>.
/// </summary>
public sealed class RecommendationRecomputeJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerManager _logger;

    public RecommendationRecomputeJob(
        IServiceScopeFactory scopeFactory,
        ILoggerManager logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <summary>
    /// Executes the recommendation sweep. Creates a fresh DI scope with its own
    /// <see cref="IRecommendationService"/> (Hangfire worker has no HTTP request scope).
    ///
    /// <c>[DisableConcurrentExecution]</c> prevents two instances of this daily job from
    /// overlapping if a run is delayed or retried. Mirrors <c>StudentProfileRecomputeJob</c>.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task Execute(CancellationToken ct = default)
    {
        var today   = DateOnly.FromDateTime(DateTime.UtcNow);
        var utcNow  = DateTime.UtcNow;

        await using var scope          = _scopeFactory.CreateAsyncScope();
        var repository                 = scope.ServiceProvider.GetRequiredService<ILearningRepository>();
        var recommendationService      = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
        var dbContext                  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        // Sweep all students that have a behavioral profile (proxy for "active / has data").
        // This mirrors how SP-Recompute sweeps stale profiles: both are grade-agnostic sweeps.
        var studentIds = await repository.GetStudentIdsWithProfileAsync(ct);

        int updatedCount = 0;
        int errorCount   = 0;

        foreach (var studentId in studentIds)
        {
            try
            {
                // ComputeAndUpsertAsync stages the upsert; we commit per-student below.
                await recommendationService.ComputeAndUpsertAsync(studentId, today, ct);

                // Commit per student — outside the UoW pipeline (see class-level doc).
                // userId = 0 (system action; no authenticated user in a background job).
                await dbContext.SaveChangesAsync(userId: 0);

                updatedCount++;
            }
            catch (Exception ex)
            {
                errorCount++;
                _logger.LogError(ex,
                    $"P5-09: RecommendationRecomputeJob — update failed for studentId={studentId}.");
            }
        }

        _logger.LogInfo(
            $"P5-09: Rec-Recompute complete — total={studentIds.Count}, updated={updatedCount}, errors={errorCount}, utcNow={utcNow:O}.");
    }
}

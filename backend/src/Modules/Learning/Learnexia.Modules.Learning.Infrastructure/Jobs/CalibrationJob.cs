using Hangfire;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Learning.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job — runs daily (<c>"20 0 * * *"</c>, fixed ID <c>"Calibrate"</c>).
///
/// Purpose (P5-07 BE-1/BE-2/BE-3): sweeps all <c>StudentAnswer</c> rows grouped by
/// <c>QuestionId</c> to compute/persist per-question empirical difficulty, then raises
/// human-promotable recalibration proposals for divergent bands, then flags AI-generated
/// questions with extreme empirical p-values out of the auto-serve pool.
///
/// DE-IDENTIFICATION (AC5 — HARD GATE):
///   Logs question-level COUNTS ONLY — no StudentId, childId, or per-child data in any log line.
///   The aggregation query itself groups by QuestionId and projects counts/averages only
///   (see <c>CalibrationService.AggregateQuestionStatsAsync</c>).
///
/// AUTHORED-CONTENT RULE:
///   This job NEVER mutates <c>QuizQuestion.Difficulty</c>, <c>QuestionText</c>,
///   <c>Options</c>, or <c>CorrectAnswer</c>. The only mutation is
///   <c>QualityState + FlagReason</c> (system flag, reversible via BE-5 Clear-flag).
///
/// IDEMPOTENT: calling the job twice for the same day overwrites the same aggregate rows
///   and upserts/updates the same Pending proposals — no duplicates (partial unique index guards).
///
/// TRANSACTION NOTE: runs OUTSIDE the HTTP command pipeline — no <c>UnitOfWorkBehavior</c>
///   wrapping the execution. Each step commits via <c>LearningDbContext.SaveChangesAsync(userId=0)</c>
///   explicitly (ADR-0001 — background jobs own their own commits).
///
/// Registration: <c>AddOrUpdate("Calibrate", ...)</c> with fixed ID — Hangfire dedupes by ID
///   across restarts. Wired in <c>LearningModule.InitializeAsync</c> AFTER <c>"Rec-Recompute"</c>.
/// </summary>
public sealed class CalibrationJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerManager _logger;

    public CalibrationJob(IServiceScopeFactory scopeFactory, ILoggerManager logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <summary>
    /// Executes the three-step calibration sweep. Creates a fresh DI scope with its own
    /// <see cref="ICalibrationService"/> (Hangfire worker has no HTTP request scope).
    ///
    /// <c>[DisableConcurrentExecution]</c> prevents two instances of this daily job from
    /// overlapping if a run is delayed or retried. Mirrors <c>RecommendationRecomputeJob</c>.
    ///
    /// Logs question/proposal COUNTS only — no per-child data in any log line (AC5).
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task Execute(CancellationToken ct = default)
    {
        var utcNow = DateTime.UtcNow;

        await using var scope              = _scopeFactory.CreateAsyncScope();
        var calibrationService             = scope.ServiceProvider.GetRequiredService<ICalibrationService>();
        var dbContext                      = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        // ── Step 1: Aggregate per-question stats (BE-1) ───────────────────────────────────────────
        int statsCount   = 0;
        int statsErrors  = 0;
        try
        {
            statsCount = await calibrationService.AggregateQuestionStatsAsync(ct);

            // Commit aggregation step (system actor — userId=0; outside UoW pipeline per ADR-0001).
            await dbContext.SaveChangesAsync(userId: 0);
        }
        catch (Exception ex)
        {
            statsErrors++;
            _logger.LogError(ex, "P5-07: CalibrationJob — AggregateQuestionStatsAsync failed.");
        }

        // ── Step 2: Upsert divergence proposals (BE-2) ───────────────────────────────────────────
        int proposalCount  = 0;
        int proposalErrors = 0;
        try
        {
            proposalCount = await calibrationService.UpsertDivergenceProposalsAsync(ct);
            await dbContext.SaveChangesAsync(userId: 0);
        }
        catch (Exception ex)
        {
            proposalErrors++;
            _logger.LogError(ex, "P5-07: CalibrationJob — UpsertDivergenceProposalsAsync failed.");
        }

        // ── Step 3: Apply AI quality flags (BE-3) ─────────────────────────────────────────────────
        int flagCount  = 0;
        int flagErrors = 0;
        try
        {
            flagCount = await calibrationService.ApplyAiFlagsAsync(ct);
            await dbContext.SaveChangesAsync(userId: 0);
        }
        catch (Exception ex)
        {
            flagErrors++;
            _logger.LogError(ex, "P5-07: CalibrationJob — ApplyAiFlagsAsync failed.");
        }

        // ── Summary log — question/proposal COUNTS only (no per-child data — AC5) ────────────────
        _logger.LogInfo(
            $"P5-07: Calibrate complete — " +
            $"statsProcessed={statsCount}, statsErrors={statsErrors}, " +
            $"proposalsUpserted={proposalCount}, proposalErrors={proposalErrors}, " +
            $"flagsChanged={flagCount}, flagErrors={flagErrors}, " +
            $"utcNow={utcNow:O}.");
    }
}

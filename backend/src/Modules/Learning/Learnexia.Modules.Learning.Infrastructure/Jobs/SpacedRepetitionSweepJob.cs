using Hangfire;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
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
    /// Maximum number of top-N skills included in each <see cref="ReviewDueIntegrationEvent"/> digest.
    /// Kept as a producer constant (mirrors <c>MaxWeakAreasSnapshot = 5</c> in
    /// <c>WeeklyReportGeneratorService</c>). Not worth a config knob at this cap.
    /// </summary>
    private const int TopSkillsCap = 3;

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
        var publisher              = scope.ServiceProvider.GetRequiredService<IPublisher>();

        // Load all due rows (NeedsReview OR Mastered+stale) — AsNoTracking.
        // GetDueMasteryRowsAsync already does .Include(m => m.Skill) so Skill.Name and
        // Skill.EstimatedTimeMinutes are available in-memory without an extra query.
        var dueRows = await repository.GetDueMasteryRowsAsync(utcNow, ct);

        int updatedCount       = 0;
        int errorCount         = 0;
        int eventsPublished    = 0;
        int eventPublishFailed = 0;

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

        // ── P3-10: Publish one ReviewDueIntegrationEvent digest per student (post-write, fail-soft) ──
        // All SR column writes above use ExecuteUpdateAsync which commits immediately (no UoW,
        // rule 3). This publish block runs after the full update loop — guaranteed post-commit.
        // An empty dueRows list produces no groups, so no events are published (AC4 guard).
        if (dueRows.Count > 0)
        {
            var byStudent = dueRows.GroupBy(r => r.StudentId);

            foreach (var studentGroup in byStudent)
            {
                try
                {
                    var studentId = studentGroup.Key;
                    var rows      = studentGroup.ToList();

                    // Top-N urgency ordering (deterministic, no extra query):
                    //   1. NeedsReview rows first (most actionable — student is actively struggling).
                    //   2. Within each status bucket, oldest NextReviewDueAt first (most overdue).
                    //      NeedsReview rows may have null NextReviewDueAt (set to MinValue for ordering).
                    var topSkills = rows
                        .OrderBy(r => r.Status == MasteryStatus.NeedsReview ? 0 : 1)
                        .ThenBy(r => r.NextReviewDueAt ?? DateTime.MinValue)
                        .Take(TopSkillsCap)
                        .Select(r => new DueSkillSnapshot(
                            SkillId:              r.SkillId,
                            SkillName:            r.Skill.Name,
                            EstimatedTimeMinutes: r.Skill.EstimatedTimeMinutes))
                        .ToList();

                    await publisher.Publish(new ReviewDueIntegrationEvent(
                        EventId:       Guid.NewGuid(),
                        OccurredOnUtc: utcNow,
                        StudentId:     studentId,
                        DueCount:      rows.Count,
                        TopSkills:     topSkills), ct);

                    eventsPublished++;
                }
                catch (Exception ex)
                {
                    eventPublishFailed++;
                    _logger.LogError(ex,
                        $"P3-10: SpacedRepetitionSweepJob — ReviewDueIntegrationEvent publish failed for studentId={studentGroup.Key}. " +
                        $"SR column writes already committed; nudge may be missed this sweep.");
                }
            }
        }

        _logger.LogInfo(
            $"P3-10: SR-Sweep complete — dueRows={dueRows.Count}, updated={updatedCount}, errors={errorCount}, " +
            $"eventsPublished={eventsPublished}, eventPublishFailed={eventPublishFailed}, utcNow={utcNow:O}.");
    }
}

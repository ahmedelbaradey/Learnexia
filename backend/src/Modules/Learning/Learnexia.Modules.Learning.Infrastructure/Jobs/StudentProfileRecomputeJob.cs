using Hangfire;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Services;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Learning.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job — runs daily (<c>Cron.Daily()</c>, fixed ID <c>"SP-Recompute"</c>).
///
/// Purpose (P3-13, AC2): defensive observability sweep. For each <c>StudentLearningProfile</c>
/// row whose <c>LastRecomputedAt</c> is older than 24 hours or null (never recomputed), this
/// job calls <c>IStudentProfileService.RecomputeProfile</c> to derive fresh behavioral attributes.
///
/// IDEMPOTENT: recomputing a profile twice from the same underlying data produces the same
/// output (<c>StudentProfileEngine</c> is a pure function; same inputs → same outputs).
///
/// TRANSACTION NOTE: this job runs OUTSIDE the HTTP command pipeline — there is no
/// <c>UnitOfWorkBehavior</c> wrapping the execution. Each call to <c>RecomputeProfile</c> stages
/// changes via <c>UpsertStudentLearningProfileAsync</c>. After each student's profile is updated
/// this job calls <c>LearningDbContext.SaveChangesAsync(userId=0)</c> explicitly to commit the
/// staged changes before moving to the next student. This is intentional: per ADR 0001, the
/// deferred-commit pattern is for HTTP commands only; background jobs own their own commits.
///
/// GRADE-TRANSITION PRESERVATION: <c>GetStaleProfilesAsync</c> applies NO grade filter — the
/// profile is per-student and grade-agnostic. A grade change must NOT reset the profile.
///
/// Registration: <c>RecurringJob.AddOrUpdate("SP-Recompute", ...)</c> with fixed ID
/// <c>"SP-Recompute"</c> — Hangfire dedupes by ID across restarts. Wired in
/// <c>LearningModule.InitializeAsync</c>, mirroring <c>SpacedRepetitionSweepJob</c>.
///
/// Hangfire resolves this class via DI — registered as Transient in
/// <c>AddLearningInfrastructure</c>; creates its own inner scope via
/// <see cref="IServiceScopeFactory"/>.
/// </summary>
public sealed class StudentProfileRecomputeJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerManager _logger;

    public StudentProfileRecomputeJob(
        IServiceScopeFactory scopeFactory,
        ILoggerManager logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <summary>
    /// Executes the sweep. Creates a fresh DI scope with its own <see cref="IStudentProfileService"/>
    /// (Hangfire worker has no HTTP request scope — a new scope is mandatory).
    ///
    /// <c>[DisableConcurrentExecution]</c> prevents two instances of this daily job from overlapping
    /// if a run is delayed or retried. Mirrors SpacedRepetitionSweepJob pattern.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task Execute(CancellationToken ct = default)
    {
        var utcNow  = DateTime.UtcNow;
        var cutoff  = utcNow.AddHours(-24);

        await using var scope    = _scopeFactory.CreateAsyncScope();
        var repository           = scope.ServiceProvider.GetRequiredService<ILearningRepository>();
        var profileService       = scope.ServiceProvider.GetRequiredService<IStudentProfileService>();
        var dbContext            = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        // Fetch all stale profiles (LastRecomputedAt > 24h ago or null). Grade-agnostic.
        var staleProfiles = await repository.GetStaleProfilesAsync(cutoff, ct);

        int updatedCount = 0;
        int errorCount   = 0;

        foreach (var profile in staleProfiles)
        {
            try
            {
                // RecomputeProfile stages changes via UpsertStudentLearningProfileAsync.
                // The service internally catches exceptions and logs them — we still catch
                // here so a single student failure does not abort the entire sweep.
                await profileService.RecomputeProfile(profile.StudentId, ct);

                // Commit per student — outside the UoW pipeline (see class-level doc).
                // userId = 0 (system action; no authenticated user in a background job).
                await dbContext.SaveChangesAsync(userId: 0);

                updatedCount++;
            }
            catch (Exception ex)
            {
                errorCount++;
                _logger.LogError(ex,
                    $"P3-13: StudentProfileRecomputeJob — update failed for studentId={profile.StudentId}.");
            }
        }

        _logger.LogInfo(
            $"P3-13: SP-Recompute complete — stale={staleProfiles.Count}, updated={updatedCount}, errors={errorCount}, utcNow={utcNow:O}.");
    }
}

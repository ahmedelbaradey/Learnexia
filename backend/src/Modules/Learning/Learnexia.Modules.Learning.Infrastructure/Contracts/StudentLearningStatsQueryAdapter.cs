using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Learning;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Learning.Infrastructure.Contracts;

/// <summary>
/// Implements <see cref="IStudentLearningStatsQuery"/> by querying <see cref="LearningDbContext"/>
/// for windowed learning statistics required by the Parent analytics API (P5-08-BE-2).
///
/// <para>Cross-module isolation: the Parent module injects <see cref="IStudentLearningStatsQuery"/>
/// from <c>Shared.Contracts</c> — it never references this adapter or any Learning project
/// directly. This adapter lives in <c>Learning.Infrastructure</c> and is registered in
/// <c>AddLearningInfrastructure</c>.</para>
///
/// <para>All results are sentinel-safe: brand-new student / empty window returns zeroed stats,
/// never null (AC3).</para>
/// </summary>
public sealed class StudentLearningStatsQueryAdapter : IStudentLearningStatsQuery
{
    private readonly LearningDbContext _db;

    public StudentLearningStatsQueryAdapter(LearningDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<LearningStats> GetStatsAsync(
        int studentId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        // Fetch all completed Attempts for this student within the window.
        // CompletedAt is nullable — only non-null completed attempts contribute.
        var completedAttempts = await _db.Attempts
            .AsNoTracking()
            .Where(a => a.StudentId == studentId
                     && a.Status == AttemptStatus.Completed
                     && a.CompletedAt.HasValue
                     && a.CompletedAt.Value >= fromUtc
                     && a.CompletedAt.Value < toUtc)
            .Select(a => new
            {
                a.LessonId,
                a.DurationSeconds,
                a.AccuracyPercentage,
            })
            .ToListAsync(ct);

        if (completedAttempts.Count == 0)
            return new LearningStats(
                LessonsCompleted:    0,
                TotalAttempts:       0,
                TimeLearningSeconds: 0,
                AvgAccuracy:         0.0);

        int lessonsCompleted    = completedAttempts.Select(a => a.LessonId).Distinct().Count();
        int timeLearningSeconds = completedAttempts.Sum(a => a.DurationSeconds);
        double avgAccuracy      = completedAttempts.Average(a => a.AccuracyPercentage);

        // TotalAttempts = count of completed attempt records in the window (matches brief definition).
        return new LearningStats(
            LessonsCompleted:    lessonsCompleted,
            TotalAttempts:       completedAttempts.Count,
            TimeLearningSeconds: timeLearningSeconds,
            AvgAccuracy:         Math.Round(avgAccuracy, 2));
    }

    /// <inheritdoc/>
    public async Task<DateTime?> GetLastActivityUtcAsync(int studentId, CancellationToken ct = default)
        => await _db.Attempts
            .AsNoTracking()
            .Where(a => a.StudentId == studentId
                     && a.Status == AttemptStatus.Completed
                     && a.CompletedAt.HasValue)
            .OrderByDescending(a => a.CompletedAt)
            .Select(a => a.CompletedAt)
            .FirstOrDefaultAsync(ct);
}

using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

/// <summary>
/// Infrastructure implementation of <see cref="IStartAttemptService"/> (Stage 6 — Option C).
///
/// Encapsulates all EF reads required by StartAttemptCommandHandler:
/// lesson existence/state guard, Lesson→Unit→Subject walk for the P8-03 language guard,
/// in-progress attempt detection, and question candidate pool loading.
///
/// All results are fully materialized — no IQueryable crosses the service boundary.
/// No SaveChangesAsync — read-only. The actual attempt creation (StartNewAsync) remains
/// in AttemptService as the pre-existing persist-and-read-Id escape hatch.
/// </summary>
public class StartAttemptService : IStartAttemptService
{
    private readonly LearningDbContext _dbContext;

    public StartAttemptService(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<Lesson?> GetPublishedActiveLessonAsync(int lessonId, CancellationToken ct = default)
        => await _dbContext.Lessons
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.Id == lessonId && l.IsActive && l.LifecycleState == LifecycleState.Published,
                ct);

    /// <inheritdoc/>
    public async Task<Subject?> GetOwningSubjectForLessonAsync(int lessonId, CancellationToken ct = default)
        => await _dbContext.Units
            .AsNoTracking()
            .Where(u => _dbContext.Lessons.Any(l => l.Id == lessonId && l.UnitId == u.Id))
            .Include(u => u.Subject)
            .Select(u => u.Subject)
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc/>
    public async Task<Attempt?> GetInProgressAttemptAsync(int studentId, int lessonId, CancellationToken ct = default)
        => await _dbContext.Attempts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.StudentId == studentId
                  && a.LessonId == lessonId
                  && a.Status == AttemptStatus.InProgress,
                ct);

    /// <inheritdoc/>
    /// <remarks>
    /// P5-07 BE-3 SERVE-GATE (single enforcement point):
    /// Adds <c>&amp;&amp; q.QualityState == QuestionQualityState.Approved</c> alongside the existing
    /// <c>IsActive &amp;&amp; LifecycleState == Published</c> filters.
    ///
    /// Questions flagged by the calibration job (<c>QualityState == FlaggedForReview</c>) are
    /// excluded from the auto-serve candidate pool. In-flight attempts that already served a
    /// question are NOT affected — <c>AttemptWriteService</c> / <c>GetQuestionForAnswerAsync</c>
    /// do NOT apply this filter so scoring of already-served questions continues normally.
    ///
    /// This is the ONLY place the QualityState filter is applied. Admins can re-enable a question
    /// via <c>ClearFlagCommand</c> (BE-5).
    /// </remarks>
    public async Task<List<QuizQuestion>> GetPublishedActiveQuestionsAsync(int lessonId, CancellationToken ct = default)
        => await _dbContext.QuizQuestions
            .AsNoTracking()
            .Where(q => q.LessonId == lessonId
                     && q.IsActive
                     && q.LifecycleState == LifecycleState.Published
                     && q.QualityState == QuestionQualityState.Approved)
            .ToListAsync(ct);
}

using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

/// <summary>
/// Infrastructure implementation of <see cref="IProgressService"/>.
/// Encapsulates the two-step query-then-stage pattern for Math/Science Attempt deletion (P8-04).
/// Stages deletes only — the UnitOfWorkBehavior commits after the handler returns (ADR 0001).
/// No SaveChangesAsync is called here.
/// </summary>
public class ProgressService : LearningBaseService<Attempt>, IProgressService
{
    public ProgressService(
        ILearningRepository repository,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer)
        : base(repository, mapper, localizer)
    {
    }

    /// <inheritdoc />
    public async Task<(bool HadCurriculumLessons, int DeletedAttempts)> ResetMathScienceProgressAsync(int studentId, CancellationToken ct = default)
    {
        // Step A — Collect all Lesson IDs whose Subject.SubjectCode is MATH or SCIENCE.
        // Traversal: Lesson.Unit.Subject.SubjectCode ∈ {MATH, SCIENCE}.
        // AsNoTracking — only Attempt rows need tracking for deletion.
        var mathScienceLessonIds = await _repository
            .GetByCondition<Lesson>(
                l => l.Unit.Subject.SubjectCode == SubjectCode.MATH
                  || l.Unit.Subject.SubjectCode == SubjectCode.SCIENCE,
                trackChanges: false)
            .Select(l => l.Id)
            .ToListAsync(ct);

        // No Math/Science lessons in the curriculum yet — distinct diagnostic case from
        // "student has no attempts" (same success/DB outcome, different operational meaning).
        if (mathScienceLessonIds.Count == 0)
            return (HadCurriculumLessons: false, DeletedAttempts: 0);

        var lessonIdSet = mathScienceLessonIds.ToHashSet();

        // Step B — Load Attempt rows with tracking (required for EF RemoveRange).
        var attemptsToDelete = await _repository
            .GetByCondition<Attempt>(
                a => a.StudentId == studentId && lessonIdSet.Contains(a.LessonId),
                trackChanges: true)
            .ToListAsync(ct);

        if (attemptsToDelete.Count == 0)
            return (HadCurriculumLessons: true, DeletedAttempts: 0);

        // Stage the deletes. UnitOfWorkBehavior commits; StudentAnswer rows cascade via
        // DeleteBehavior.Cascade in StudentAnswerConfig — no explicit StudentAnswer delete needed.
        await _repository.DeleteRangeAsync(attemptsToDelete);

        return (HadCurriculumLessons: true, DeletedAttempts: attemptsToDelete.Count);
    }
}

using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

/// <summary>
/// Service for <see cref="Attempt"/> aggregates. Extends <see cref="LearningBaseService{TEntity}"/>
/// for the standard deferred-commit reads/writes, and provides <see cref="StartNewAsync"/> which
/// must commit immediately so the DB-generated <c>Id</c> is available for the response.
/// Mirrors the precedent set by <c>LinkParentStudentService</c> (Parent module) for
/// "persist-and-read-Id" scenarios.
/// </summary>
public class AttemptService : LearningBaseService<Attempt>, IAttemptService
{
    private readonly LearningDbContext _dbContext;

    public AttemptService(
        ILearningRepository repository,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer,
        LearningDbContext dbContext)
        : base(repository, mapper, localizer)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<Attempt> StartNewAsync(int studentId, int lessonId, CancellationToken cancellationToken = default)
    {
        var attempt = new Attempt
        {
            StudentId          = studentId,
            LessonId           = lessonId,
            Status             = AttemptStatus.InProgress,
            StartedAt          = DateTime.UtcNow,
            AccuracyPercentage = 0,
            DurationSeconds    = 0,
            HintsUsedCount     = 0,
            CompletedAt        = null
        };

        await _dbContext.Attempts.AddAsync(attempt, cancellationToken);
        // Commit immediately so the DB-generated Id is populated before the response is built.
        // The UnitOfWorkBehavior's subsequent SaveChanges is a harmless no-op for this entity.
        await _dbContext.SaveChangesAsync(studentId);

        return attempt;
    }
}

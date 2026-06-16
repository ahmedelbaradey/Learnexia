using System.Text.Json;
using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

// Disambiguate Learning.Domain.Entities.Unit from MediatR.Unit
using LearningUnit = Learnexia.Modules.Learning.Domain.Entities.Unit;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

/// <summary>
/// Infrastructure implementation of <see cref="ILifecycleService"/>.
/// Encapsulates all EF reads (IgnoreQueryFilters, GetByCondition, FirstOrDefaultAsync, etc.)
/// and snapshot serialization logic for the content lifecycle and versioning features (P7-05).
/// All write methods stage changes only — the UnitOfWorkBehavior commits after the handler returns
/// (ADR 0001). No SaveChangesAsync is called here.
/// </summary>
public class LifecycleService : LearningBaseService<ContentVersion>, ILifecycleService
{
    public LifecycleService(
        ILearningRepository repository,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer)
        : base(repository, mapper, localizer)
    {
    }

    // ── Rollback reads ──────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ContentVersion?> GetVersionAsync(
        VersionedEntityType entityType, int entityId, int versionNumber,
        CancellationToken ct = default)
        => await _repository.GetVersionAsync(entityType, entityId, versionNumber, ct);

    /// <inheritdoc />
    public async Task<Subject?> GetTrackedSubjectIgnoreFiltersAsync(int id, CancellationToken ct = default)
        => await _repository.GetByCondition<Subject>(s => s.Id == id, trackChanges: true)
                            .IgnoreQueryFilters()
                            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<LearningUnit?> GetTrackedUnitIgnoreFiltersAsync(int id, CancellationToken ct = default)
        => await _repository.GetByCondition<LearningUnit>(u => u.Id == id, trackChanges: true)
                            .IgnoreQueryFilters()
                            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<Lesson?> GetTrackedLessonIgnoreFiltersAsync(int id, CancellationToken ct = default)
        => await _repository.GetByCondition<Lesson>(l => l.Id == id, trackChanges: true)
                            .IgnoreQueryFilters()
                            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<QuizQuestion?> GetTrackedQuizQuestionIgnoreFiltersAsync(int id, CancellationToken ct = default)
        => await _repository.GetByCondition<QuizQuestion>(q => q.Id == id, trackChanges: true)
                            .IgnoreQueryFilters()
                            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task StageEntityUpdateAsync<TEntity>(TEntity entity, CancellationToken ct = default) where TEntity : class
        => await _repository.UpdateAsync(entity);

    /// <inheritdoc />
    public async Task<int> GetMaxVersionNumberAsync(
        VersionedEntityType entityType, int entityId, CancellationToken ct = default)
        => await _repository.GetMaxVersionNumberAsync(entityType, entityId, ct);

    /// <inheritdoc />
    public async Task<Subject?> GetOwningSubjectAsync(
        VersionedEntityType entityType, int entityId, CancellationToken ct = default)
        => await _repository.GetOwningSubjectAsync(entityType, entityId, ct);

    /// <inheritdoc />
    public async Task StageAddContentVersionAsync(ContentVersion version, CancellationToken ct = default)
        => await _repository.AddAsync(version, ct);

    // ── TransitionLifecycle: snapshot serialization ────────────────────────────────────────────────

    /// <inheritdoc />
    /// <remarks>
    /// Serializes a whitelist-DTO of only the editorial fields — symmetric with the
    /// ApplyXxxSnapshot rollback helpers in RollbackToVersionCommandHandler.
    /// This ensures snapshot ↔ rollback are symmetric: the same field names are used by
    /// both the publish snapshot and the rollback apply paths.
    /// </remarks>
    public string SerializeSnapshot(VersionedEntityType entityType, object entity)
    {
        return entityType switch
        {
            VersionedEntityType.Subject when entity is Subject s => JsonSerializer.Serialize(new
            {
                s.Name,
                s.Country,
                SubjectCode = (int)s.SubjectCode,
                Language    = (int)s.Language,
                s.SequenceOrder,
                s.IsActive,
            }),
            VersionedEntityType.Unit when entity is LearningUnit u => JsonSerializer.Serialize(new
            {
                u.Name,
                u.SequenceOrder,
                u.IsActive,
            }),
            VersionedEntityType.Lesson when entity is Lesson l => JsonSerializer.Serialize(new
            {
                l.Name,
                Difficulty     = (int)l.Difficulty,
                l.SequenceOrder,
                l.IsLocked,
                l.IsActive,
                l.EstimatedMinutes,
                l.Explanation,
                l.Visual,
                l.IsBoss,
            }),
            VersionedEntityType.QuizQuestion when entity is QuizQuestion q => JsonSerializer.Serialize(new
            {
                q.QuestionText,
                QuestionType = (int)q.QuestionType,
                q.Options,
                q.CorrectAnswer,
                Difficulty   = (int)q.Difficulty,
                GeneratedBy  = (int)q.GeneratedBy,
                q.SequenceOrder,
                q.IsActive,
            }),
            _ => throw new InvalidOperationException(
                $"No snapshot whitelist defined for EntityType={entityType}"),
        };
    }

    // ── GetPreview reads ────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<(LifecycleState State, string Snapshot)?> GetPreviewSubjectAsync(int id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByCondition<Subject>(s => s.Id == id, trackChanges: false)
                                      .IgnoreQueryFilters()
                                      .FirstOrDefaultAsync(ct);
        if (entity is null) return null;
        return (entity.LifecycleState, JsonSerializer.Serialize(entity));
    }

    /// <inheritdoc />
    public async Task<(LifecycleState State, string Snapshot)?> GetPreviewUnitAsync(int id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByCondition<LearningUnit>(u => u.Id == id, trackChanges: false)
                                      .IgnoreQueryFilters()
                                      .FirstOrDefaultAsync(ct);
        if (entity is null) return null;
        return (entity.LifecycleState, JsonSerializer.Serialize(entity));
    }

    /// <inheritdoc />
    public async Task<(LifecycleState State, string Snapshot)?> GetPreviewLessonAsync(int id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByCondition<Lesson>(l => l.Id == id, trackChanges: false)
                                      .IgnoreQueryFilters()
                                      .FirstOrDefaultAsync(ct);
        if (entity is null) return null;
        return (entity.LifecycleState, JsonSerializer.Serialize(entity));
    }

    /// <inheritdoc />
    public async Task<(LifecycleState State, string Snapshot)?> GetPreviewQuizQuestionAsync(int id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByCondition<QuizQuestion>(q => q.Id == id, trackChanges: false)
                                      .IgnoreQueryFilters()
                                      .FirstOrDefaultAsync(ct);
        if (entity is null) return null;
        return (entity.LifecycleState, JsonSerializer.Serialize(entity));
    }

    // ── GetPublicationCoverage reads ───────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Grade?> GetGradeByIdNoTrackingAsync(int gradeId, CancellationToken ct = default)
        => await _repository.GetAll<Grade>(trackChanges: false)
                            .FirstOrDefaultAsync(g => g.Id == gradeId, ct);

    /// <inheritdoc />
    public async Task<List<Subject>> GetSubjectsForCoverageAsync(int gradeId, CancellationToken ct = default)
        => await _repository.GetSubjectsForCoverageAsync(gradeId, ct);

    // ── GetVersionHistory reads ────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<List<ContentVersion>> GetVersionHistoryAsync(
        VersionedEntityType entityType, int entityId, CancellationToken ct = default)
        => await _repository.GetVersionHistoryAsync(entityType, entityId, ct);
}

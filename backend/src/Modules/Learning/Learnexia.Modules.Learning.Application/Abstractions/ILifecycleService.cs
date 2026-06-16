using Learnexia.Modules.Learning.Application.Features.Lifecycle.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Learning.Application.Abstractions;

// Disambiguate Learning.Domain.Entities.Unit from MediatR.Unit
using LearningUnit = Domain.Entities.Unit;

/// <summary>
/// Service seam for all content lifecycle and versioning operations (P7-05, P7-12).
/// All EF queries, IgnoreQueryFilters calls, IncludeQueryFilters, and serialization-helpers
/// are encapsulated here — Application handlers inject this interface only (Option C).
/// No IQueryable / DbSet / EF extension methods cross this boundary.
/// </summary>
public interface ILifecycleService
{
    // ── Rollback reads ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the <see cref="ContentVersion"/> row for (entityType, entityId, versionNumber), or null.
    /// Used by RollbackToVersionCommandHandler to load the snapshot to apply.
    /// </summary>
    Task<ContentVersion?> GetVersionAsync(
        VersionedEntityType entityType, int entityId, int versionNumber,
        CancellationToken ct = default);

    /// <summary>
    /// Loads a tracked <see cref="Subject"/> for the given id, ignoring query filters (IgnoreQueryFilters),
    /// so that soft-deleted or draft/archived rows are visible. Returns null when not found.
    /// </summary>
    Task<Subject?> GetTrackedSubjectIgnoreFiltersAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Loads a tracked <see cref="LearningUnit"/> for the given id, ignoring query filters.
    /// Returns null when not found.
    /// </summary>
    Task<LearningUnit?> GetTrackedUnitIgnoreFiltersAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Loads a tracked <see cref="Lesson"/> for the given id, ignoring query filters.
    /// Returns null when not found.
    /// </summary>
    Task<Lesson?> GetTrackedLessonIgnoreFiltersAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Loads a tracked <see cref="QuizQuestion"/> for the given id, ignoring query filters.
    /// Returns null when not found.
    /// </summary>
    Task<QuizQuestion?> GetTrackedQuizQuestionIgnoreFiltersAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Stages an update for the already-tracked entity (calls repository UpdateAsync — no commit).
    /// The UoW behavior commits after the handler returns.
    /// </summary>
    Task StageEntityUpdateAsync<TEntity>(TEntity entity, CancellationToken ct = default) where TEntity : class;

    /// <summary>
    /// Returns the maximum VersionNumber for (entityType, entityId), or 0 when none exist.
    /// Used to determine the next version number before appending a new ContentVersion row.
    /// </summary>
    Task<int> GetMaxVersionNumberAsync(
        VersionedEntityType entityType, int entityId, CancellationToken ct = default);

    /// <summary>
    /// Returns the owning Subject (for language derivation) for the given entity type + id.
    /// AsNoTracking; returns null when not resolvable.
    /// </summary>
    Task<Subject?> GetOwningSubjectAsync(
        VersionedEntityType entityType, int entityId, CancellationToken ct = default);

    /// <summary>
    /// Stages a new <see cref="ContentVersion"/> row (AddAsync — no commit).
    /// The UoW behavior commits after the handler returns.
    /// </summary>
    Task StageAddContentVersionAsync(ContentVersion version, CancellationToken ct = default);

    // ── TransitionLifecycle: snapshot serialization ────────────────────────────────────────────────

    /// <summary>
    /// Serializes the whitelist of editorial fields for the given entity into a JSON snapshot string.
    /// The whitelist matches the ApplyXxxSnapshot helpers in RollbackToVersionCommandHandler so that
    /// publish and rollback are symmetric (same field names). entityType must match the runtime type
    /// of entity.
    /// </summary>
    string SerializeSnapshot(VersionedEntityType entityType, object entity);

    // ── GetPreview reads ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a no-tracking <see cref="Subject"/> for the given id, ignoring query filters.
    /// Returns (LifecycleState, serialized full entity) or null when not found.
    /// Used by GetPreviewQueryHandler.
    /// </summary>
    Task<(LifecycleState State, string Snapshot)?> GetPreviewSubjectAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Loads a no-tracking <see cref="LearningUnit"/> for the given id, ignoring query filters.
    /// Returns (LifecycleState, serialized full entity) or null when not found.
    /// </summary>
    Task<(LifecycleState State, string Snapshot)?> GetPreviewUnitAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Loads a no-tracking <see cref="Lesson"/> for the given id, ignoring query filters.
    /// Returns (LifecycleState, serialized full entity) or null when not found.
    /// </summary>
    Task<(LifecycleState State, string Snapshot)?> GetPreviewLessonAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Loads a no-tracking <see cref="QuizQuestion"/> for the given id, ignoring query filters.
    /// Returns (LifecycleState, serialized full entity) or null when not found.
    /// </summary>
    Task<(LifecycleState State, string Snapshot)?> GetPreviewQuizQuestionAsync(int id, CancellationToken ct = default);

    // ── GetPublicationCoverage reads ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the Grade for the given id (no tracking), or null when not found.
    /// Used by GetPublicationCoverageQueryHandler for the existence check.
    /// </summary>
    Task<Grade?> GetGradeByIdNoTrackingAsync(int gradeId, CancellationToken ct = default);

    /// <summary>
    /// Returns all non-deleted Subjects for the given GradeId (admin coverage read — no IsActive filter).
    /// Delegates to ILearningRepository.GetSubjectsForCoverageAsync.
    /// </summary>
    Task<List<Subject>> GetSubjectsForCoverageAsync(int gradeId, CancellationToken ct = default);

    // ── GetVersionHistory reads ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all <see cref="ContentVersion"/> rows for (entityType, entityId), newest first.
    /// Delegates to ILearningRepository.GetVersionHistoryAsync.
    /// </summary>
    Task<List<ContentVersion>> GetVersionHistoryAsync(
        VersionedEntityType entityType, int entityId, CancellationToken ct = default);
}

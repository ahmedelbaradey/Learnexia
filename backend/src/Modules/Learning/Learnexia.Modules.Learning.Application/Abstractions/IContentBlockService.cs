using Learnexia.Modules.Learning.Application.Features.ContentBlocks.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;

namespace Learnexia.Modules.Learning.Application.Abstractions;

/// <summary>
/// Service seam for ContentBlock persistence operations.
/// All EF/DbContext calls stay inside Infrastructure; handlers are thin orchestrators.
/// ContentBlock derives from FullAuditedEntity (not AggregateRoot) — domain events are raised
/// on the parent Lesson aggregate inside the handler, not inside this service.
/// </summary>
public interface IContentBlockService
{
    // ── Add path ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the tracked Lesson for the given id (change-tracking ON), or null when not found.
    /// Used by Add/Delete/Edit/Reorder handlers that must raise a domain event on the parent Lesson.
    /// </summary>
    Task<Lesson?> GetLessonTrackedAsync(int lessonId, CancellationToken ct = default);

    /// <summary>
    /// Returns the current maximum SequenceOrder for content blocks belonging to the given lesson,
    /// or -1 when no blocks exist (so the first block gets SequenceOrder = 0).
    /// Used by AddContentBlockCommandHandler for append semantics (max + 1).
    /// </summary>
    Task<int> GetMaxSequenceOrderAsync(int lessonId, CancellationToken ct = default);

    /// <summary>
    /// Stages the ContentBlock via AddAsync. No SaveChangesAsync — the UoW behavior commits.
    /// </summary>
    Task StageContentBlockAddAsync(ContentBlock block, CancellationToken ct = default);

    // ── Delete / Edit path ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the tracked ContentBlock for the given id (change-tracking ON), or null when not found.
    /// The caller mutates the entity and the UoW behavior commits the change.
    /// </summary>
    Task<ContentBlock?> GetContentBlockTrackedAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Stages an update to the already-tracked ContentBlock entity (calls repository UpdateAsync).
    /// No SaveChangesAsync — the UoW behavior commits after the handler.
    /// </summary>
    Task StageContentBlockUpdateAsync(ContentBlock block, CancellationToken ct = default);

    // ── Reorder path ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads and returns tracked ContentBlock entities for all given ids.
    /// The handler applies new SequenceOrder values and stages UpdateAsync calls; the UoW commits.
    /// </summary>
    Task<List<ContentBlock>> GetContentBlocksTrackedByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);

    // ── List path ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>Returns true when a Lesson with the given id exists (global IsDeleted filter active).</summary>
    Task<bool> LessonExistsAsync(int lessonId, CancellationToken ct = default);

    /// <summary>
    /// Returns all non-deleted ContentBlocks for the given lesson ordered by SequenceOrder,
    /// materialized. Global IsDeleted filter applies (deleted blocks excluded automatically).
    /// Admin view — IsActive NOT filtered, so inactive blocks are included.
    /// </summary>
    Task<List<AdminContentBlockDto>> GetContentBlocksOrderedAsync(int lessonId, CancellationToken ct = default);
}

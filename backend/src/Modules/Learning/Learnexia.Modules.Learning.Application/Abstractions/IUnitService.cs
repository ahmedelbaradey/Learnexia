using Learnexia.Modules.Learning.Application.Features.Units.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;
using LearningUnit = Learnexia.Modules.Learning.Domain.Entities.Unit;

namespace Learnexia.Modules.Learning.Application.Abstractions;

public interface IUnitService : IBaseService<LearningUnit>
{
    // ── Add path ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>Returns true when a Subject with the given id exists.</summary>
    Task<bool> SubjectExistsAsync(int subjectId, CancellationToken ct = default);

    /// <summary>
    /// Stages the Unit via AddAsync then FlushAsync within the UoW transaction so the DB assigns
    /// a primary-key Id. Returns the same tracked instance so the caller can raise a domain event.
    /// </summary>
    Task<LearningUnit> StageAddUnitAsync(LearningUnit unit, int userId, CancellationToken ct = default);

    // ── Delete / SetActive path ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the tracked Unit for the given id (EF change-tracking ON), or null when not found.
    /// The caller mutates the entity and the UoW behavior commits the change.
    /// </summary>
    Task<LearningUnit?> GetUnitTrackedAsync(int id, CancellationToken ct = default);

    /// <summary>Returns true when the Unit has at least one non-deleted Lesson.</summary>
    Task<bool> UnitHasLessonsAsync(int unitId, CancellationToken ct = default);

    /// <summary>
    /// Stages an update to the already-tracked Unit entity (calls repository UpdateAsync).
    /// No SaveChangesAsync — the UoW behavior commits after the handler.
    /// </summary>
    Task StageUnitUpdateAsync(LearningUnit unit, CancellationToken ct = default);

    // ── Reorder path ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads and returns tracked Unit entities for all given ids. The handler applies new
    /// SequenceOrder values and stages UpdateAsync calls; the UoW commits them atomically.
    /// </summary>
    Task<List<LearningUnit>> GetUnitsTrackedByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);

    // ── List/pagination ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a paginated, materialized list of Units projected to <see cref="SingleUnitResponse"/>.
    /// When <paramref name="subjectId"/> is provided only units for that subject are returned.
    /// All EF + ProjectTo + pagination composition stays inside Infrastructure.
    /// </summary>
    Task<PaginatedResult<SingleUnitResponse>> GetPagedAsync(
        int? subjectId,
        int pageNumber,
        int pageSize,
        string? orderBy,
        CancellationToken ct = default);
}

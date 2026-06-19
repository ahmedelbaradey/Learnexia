using Learnexia.Modules.Learning.Application.Features.Grades.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Abstractions;

public interface IGradeService : IBaseService<Grade>
{
    /// <summary>
    /// Returns a paginated, materialized list of grades projected to <see cref="SingleGradeResponse"/>.
    /// Composes the EF IQueryable, ProjectTo, and ToPaginatedListAsync entirely inside Infrastructure
    /// so no EF types cross the Application boundary.
    /// </summary>
    Task<PaginatedResult<SingleGradeResponse>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? orderBy,
        CancellationToken cancellationToken = default);

    // ── Delete / Edit path ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the tracked Grade for the given id (EF change-tracking ON), or null when not found.
    /// The caller mutates the entity and the UoW behavior commits the change.
    /// </summary>
    Task<Grade?> GetGradeTrackedAsync(int id, CancellationToken ct = default);

    /// <summary>Returns true when the Grade has at least one non-deleted Subject.</summary>
    Task<bool> GradeHasSubjectsAsync(int gradeId, CancellationToken ct = default);
}

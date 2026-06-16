using Learnexia.Modules.Learning.Application.Features.Concepts.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Abstractions;

public interface IConceptService : IBaseService<Concept>
{
    /// <summary>
    /// Returns a paginated, materialized list of concepts projected to <see cref="SingleConceptResponse"/>.
    /// When <paramref name="subjectId"/> is provided, only concepts belonging to that subject are returned.
    /// Composes the EF IQueryable, ProjectTo, and ToPaginatedListAsync entirely inside Infrastructure
    /// so no EF types cross the Application boundary.
    /// </summary>
    Task<PaginatedResult<SingleConceptResponse>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? orderBy,
        int? subjectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>true</c> if a Subject with <paramref name="subjectId"/> exists (non-deleted).
    /// Used by AddConceptCommandHandler to verify the parent Subject before staging the insert.
    /// Keeps the AnyAsync EF call inside Infrastructure — Application layer stays EF-free.
    /// </summary>
    Task<bool> SubjectExistsAsync(int subjectId, CancellationToken cancellationToken = default);
}

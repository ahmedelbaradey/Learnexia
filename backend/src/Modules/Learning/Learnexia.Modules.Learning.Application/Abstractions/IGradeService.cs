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
}

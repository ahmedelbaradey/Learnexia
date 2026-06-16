using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Grades.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

public class GradeService : LearningBaseService<Grade>, IGradeService
{
    public GradeService(ILearningRepository repository, IMapper mapper, IStringLocalizer<SharedResources> localizer)
        : base(repository, mapper, localizer)
    {
    }

    /// <inheritdoc/>
    public async Task<PaginatedResult<SingleGradeResponse>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? orderBy,
        CancellationToken cancellationToken = default)
    {
        // IQueryable composition stays entirely inside Infrastructure — no EF type crosses the boundary.
        var query = _repository.GetAll<Grade>(trackChanges: false);
        if (!query.Any())
            return PaginatedResult<SingleGradeResponse>.EmptyCollection();

        return await _mapper
            .ProjectTo<SingleGradeResponse>(query)
            .ToPaginatedListAsync(pageNumber, pageSize, orderBy);
    }
}

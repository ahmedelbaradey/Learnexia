using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Concepts.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

public class ConceptService : LearningBaseService<Concept>, IConceptService
{
    public ConceptService(ILearningRepository repository, IMapper mapper, IStringLocalizer<SharedResources> localizer)
        : base(repository, mapper, localizer)
    {
    }

    /// <inheritdoc/>
    public async Task<PaginatedResult<SingleConceptResponse>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? orderBy,
        int? subjectId,
        CancellationToken cancellationToken = default)
    {
        // IQueryable composition stays entirely inside Infrastructure — no EF type crosses the boundary.
        var query = subjectId.HasValue
            ? _repository.GetByCondition<Concept>(c => c.SubjectId == subjectId.Value, trackChanges: false)
            : _repository.GetAll<Concept>(trackChanges: false);

        if (!query.Any())
            return PaginatedResult<SingleConceptResponse>.EmptyCollection();

        return await _mapper
            .ProjectTo<SingleConceptResponse>(query)
            .ToPaginatedListAsync(pageNumber, pageSize, orderBy);
    }

    /// <inheritdoc/>
    public async Task<bool> SubjectExistsAsync(int subjectId, CancellationToken cancellationToken = default)
        // Delegates to ILearningRepository.AnyAsync which owns the EF call — Infrastructure-only.
        => await _repository.AnyAsync<Subject>(s => s.Id == subjectId);
}

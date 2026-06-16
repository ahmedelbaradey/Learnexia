using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Units.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;
using LearningUnit = Learnexia.Modules.Learning.Domain.Entities.Unit;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

public class UnitService : LearningBaseService<LearningUnit>, IUnitService
{
    public UnitService(ILearningRepository repository, IMapper mapper, IStringLocalizer<SharedResources> localizer)
        : base(repository, mapper, localizer)
    {
    }

    // ── Add path ──────────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> SubjectExistsAsync(int subjectId, CancellationToken ct = default)
        => await _repository.GetAll<Subject>(trackChanges: false)
                            .AnyAsync(s => s.Id == subjectId, ct);

    /// <inheritdoc />
    public async Task<LearningUnit> StageAddUnitAsync(LearningUnit unit, int userId, CancellationToken ct = default)
    {
        await _repository.AddAsync(unit, ct);
        await _repository.FlushAsync(userId, ct);
        return unit;
    }

    // ── Delete path ───────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<LearningUnit?> GetUnitTrackedAsync(int id, CancellationToken ct = default)
        => await _repository.GetByCondition<LearningUnit>(u => u.Id == id, trackChanges: true)
                            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<bool> UnitHasLessonsAsync(int unitId, CancellationToken ct = default)
        => await _repository.GetAll<Lesson>(trackChanges: false)
                            .AnyAsync(l => l.UnitId == unitId, ct);

    /// <inheritdoc />
    public async Task StageUnitUpdateAsync(LearningUnit unit, CancellationToken ct = default)
        => await _repository.UpdateAsync(unit);

    // ── Reorder path ─────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<List<LearningUnit>> GetUnitsTrackedByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default)
        => await _repository.GetByCondition<LearningUnit>(u => ids.Contains(u.Id), trackChanges: true)
                            .ToListAsync(ct);

    // ── List/pagination ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<PaginatedResult<SingleUnitResponse>> GetPagedAsync(
        int? subjectId,
        int pageNumber,
        int pageSize,
        string? orderBy,
        CancellationToken ct = default)
    {
        var query = subjectId.HasValue
            ? _repository.GetByCondition<LearningUnit>(u => u.SubjectId == subjectId.Value, trackChanges: false)
            : _repository.GetAll<LearningUnit>(trackChanges: false);

        if (!query.Any())
            return PaginatedResult<SingleUnitResponse>.EmptyCollection();

        return await _mapper
            .ProjectTo<SingleUnitResponse>(query)
            .ToPaginatedListAsync(pageNumber, pageSize, orderBy);
    }
}

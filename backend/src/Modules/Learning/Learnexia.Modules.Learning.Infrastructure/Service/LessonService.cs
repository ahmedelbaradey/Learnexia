using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Lessons.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

public class LessonService : LearningBaseService<Lesson>, ILessonService
{
    public LessonService(ILearningRepository repository, IMapper mapper, IStringLocalizer<SharedResources> localizer)
        : base(repository, mapper, localizer)
    {
    }

    // ── Add path ──────────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> UnitExistsAsync(int unitId, CancellationToken ct = default)
        => await _repository.GetAll<Unit>(trackChanges: false)
                            .AnyAsync(u => u.Id == unitId, ct);

    /// <inheritdoc />
    public async Task<bool> SkillExistsAsync(int skillId, CancellationToken ct = default)
        => await _repository.GetAll<Skill>(trackChanges: false)
                            .AnyAsync(sk => sk.Id == skillId, ct);

    /// <inheritdoc />
    public async Task<Lesson> StageAddLessonAsync(Lesson lesson, int userId, CancellationToken ct = default)
    {
        await _repository.AddAsync(lesson, ct);
        await _repository.FlushAsync(userId, ct);
        return lesson;
    }

    // ── Edit path ─────────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Lesson?> StageEditLessonAsync<TDto>(TDto dto, CancellationToken ct = default)
        where TDto : Learnexia.Shared.Kernel.Dtos.BaseDto
    {
        var lesson = await _repository.GetByCondition<Lesson>(l => l.Id == dto.Id, trackChanges: true)
                                      .FirstOrDefaultAsync(ct);
        if (lesson is null)
            return null;

        _mapper.Map(dto, lesson);
        await _repository.UpdateAsync(lesson);
        return lesson;
    }

    // ── Delete path ───────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Lesson?> GetLessonTrackedAsync(int id, CancellationToken ct = default)
        => await _repository.GetByCondition<Lesson>(l => l.Id == id, trackChanges: true)
                            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<List<ContentBlock>> GetContentBlocksTrackedAsync(int lessonId, CancellationToken ct = default)
        => await _repository.GetByCondition<ContentBlock>(cb => cb.LessonId == lessonId, trackChanges: true)
                            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task StageLessonUpdateAsync(Lesson lesson, CancellationToken ct = default)
        => await _repository.UpdateAsync(lesson);

    /// <inheritdoc />
    public async Task StageContentBlockUpdateAsync(ContentBlock block, CancellationToken ct = default)
        => await _repository.UpdateAsync(block);

    // ── Reorder path ─────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<List<Lesson>> GetLessonsTrackedByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default)
        => await _repository.GetByCondition<Lesson>(l => ids.Contains(l.Id), trackChanges: true)
                            .ToListAsync(ct);

    // ── List/pagination ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<PaginatedResult<SingleLessonResponse>> GetPagedAsync(
        int? unitId,
        int pageNumber,
        int pageSize,
        string? orderBy,
        CancellationToken ct = default)
    {
        var query = unitId.HasValue
            ? _repository.GetByCondition<Lesson>(l => l.UnitId == unitId.Value, trackChanges: false)
            : _repository.GetAll<Lesson>(trackChanges: false);

        if (!query.Any())
            return PaginatedResult<SingleLessonResponse>.EmptyCollection();

        return await _mapper
            .ProjectTo<SingleLessonResponse>(query)
            .ToPaginatedListAsync(pageNumber, pageSize, orderBy);
    }

    // ── Student GetLesson query path ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Lesson?> GetActiveLessonAsync(int lessonId, CancellationToken ct = default)
        => await _repository
                .GetByCondition<Lesson>(
                    l => l.Id == lessonId && l.IsActive && l.LifecycleState == LifecycleState.Published,
                    trackChanges: false)
                .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<Subject?> GetOwningSubjectByUnitAsync(int unitId, CancellationToken ct = default)
        => await _repository.GetByCondition<Unit>(u => u.Id == unitId, trackChanges: false)
                            .Include(u => u.Subject)
                            .Select(u => u.Subject)
                            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<QuizQuestion?> GetFirstQuizQuestionAsync(int lessonId, CancellationToken ct = default)
        => await _repository.GetByCondition<QuizQuestion>(q => q.LessonId == lessonId, trackChanges: false)
                            .OrderBy(q => q.Id)
                            .FirstOrDefaultAsync(ct);

    // ── Admin GetLesson query path ────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Lesson?> GetAdminLessonWithContentBlocksAsync(int lessonId, CancellationToken ct = default)
        => await _repository.GetByCondition<Lesson>(l => l.Id == lessonId, trackChanges: false)
                            .Include(l => l.ContentBlocks)
                            .FirstOrDefaultAsync(ct);

    // ── Attempt-completion helper ─────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<int?> GetLessonSkillIdAsync(int lessonId, CancellationToken ct = default)
        => await _repository.GetLessonSkillIdAsync(lessonId, ct);
}

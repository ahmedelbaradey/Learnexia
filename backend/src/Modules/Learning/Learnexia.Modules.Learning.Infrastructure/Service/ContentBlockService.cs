using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.ContentBlocks.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

/// <summary>
/// Service for ContentBlock persistence operations. ContentBlock is a FullAuditedEntity child
/// of Lesson (not an AggregateRoot), so this service does NOT raise domain events — that
/// responsibility stays in the handler, on the parent Lesson aggregate.
/// All writes are staged only; the UnitOfWorkBehavior commits after the handler returns.
/// </summary>
public class ContentBlockService : LearningBaseService<ContentBlock>, IContentBlockService
{
    public ContentBlockService(ILearningRepository repository, IMapper mapper, IStringLocalizer<SharedResources> localizer)
        : base(repository, mapper, localizer)
    {
    }

    // ── Add path ──────────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Lesson?> GetLessonTrackedAsync(int lessonId, CancellationToken ct = default)
        => await _repository.GetByCondition<Lesson>(l => l.Id == lessonId, trackChanges: true)
                            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<int> GetMaxSequenceOrderAsync(int lessonId, CancellationToken ct = default)
    {
        var maxOrder = await _repository
            .GetByCondition<ContentBlock>(cb => cb.LessonId == lessonId, trackChanges: false)
            .Select(cb => (int?)cb.SequenceOrder)
            .MaxAsync(ct);

        return (maxOrder ?? -1);
    }

    /// <inheritdoc />
    public async Task StageContentBlockAddAsync(ContentBlock block, CancellationToken ct = default)
        => await _repository.AddAsync(block, ct);

    // ── Delete / Edit path ────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ContentBlock?> GetContentBlockTrackedAsync(int id, CancellationToken ct = default)
        => await _repository.GetByCondition<ContentBlock>(cb => cb.Id == id, trackChanges: true)
                            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task StageContentBlockUpdateAsync(ContentBlock block, CancellationToken ct = default)
        => await _repository.UpdateAsync(block);

    // ── Reorder path ─────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<List<ContentBlock>> GetContentBlocksTrackedByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default)
        => await _repository.GetByCondition<ContentBlock>(cb => ids.Contains(cb.Id), trackChanges: true)
                            .ToListAsync(ct);

    // ── List path ─────────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> LessonExistsAsync(int lessonId, CancellationToken ct = default)
        => await _repository.GetAll<Lesson>(trackChanges: false)
                            .AnyAsync(l => l.Id == lessonId, ct);

    /// <inheritdoc />
    public async Task<List<AdminContentBlockDto>> GetContentBlocksOrderedAsync(int lessonId, CancellationToken ct = default)
    {
        var blocks = await _repository
            .GetByCondition<ContentBlock>(cb => cb.LessonId == lessonId, trackChanges: false)
            .OrderBy(cb => cb.SequenceOrder)
            .ToListAsync(ct);

        return _mapper.Map<List<AdminContentBlockDto>>(blocks);
    }
}

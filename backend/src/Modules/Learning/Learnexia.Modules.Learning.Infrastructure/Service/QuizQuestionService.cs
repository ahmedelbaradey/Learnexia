using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

public class QuizQuestionService : LearningBaseService<QuizQuestion>, IQuizQuestionService
{
    public QuizQuestionService(ILearningRepository repository, IMapper mapper, IStringLocalizer<SharedResources> localizer)
        : base(repository, mapper, localizer)
    {
    }

    // ── Add path ──────────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> LessonExistsAsync(int lessonId, CancellationToken ct = default)
        => await _repository.AnyAsync<Lesson>(l => l.Id == lessonId);

    /// <inheritdoc />
    public async Task<int> GetMaxSequenceOrderAsync(int lessonId, CancellationToken ct = default)
    {
        // MaxAsync on an empty set returns null; we return -1 so the handler can compute next = max + 1.
        var maxOrder = await _repository
            .GetByCondition<QuizQuestion>(q => q.LessonId == lessonId, trackChanges: false)
            .Select(q => (int?)q.SequenceOrder)
            .MaxAsync(ct);

        return maxOrder ?? -1;
    }

    /// <inheritdoc />
    public async Task<QuizQuestion> StageAddQuestionAsync(QuizQuestion question, CancellationToken ct = default)
    {
        await _repository.AddAsync(question, ct);
        return question;
    }

    // ── Edit / Delete / SetActive path ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<QuizQuestion?> GetQuestionTrackedAsync(int id, CancellationToken ct = default)
        => await _repository
            .GetByCondition<QuizQuestion>(q => q.Id == id, trackChanges: true)
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task StageQuestionUpdateAsync(QuizQuestion question, CancellationToken ct = default)
        => await _repository.UpdateAsync(question);

    // ── Reorder path ─────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<List<QuizQuestion>> GetQuestionsTrackedByIdsForLessonAsync(
        IReadOnlyCollection<int> ids, int lessonId, CancellationToken ct = default)
        => await _repository
            .GetByCondition<QuizQuestion>(
                q => ids.Contains(q.Id) && q.LessonId == lessonId,
                trackChanges: true)
            .ToListAsync(ct);

    // ── Query path ───────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<QuizQuestion?> GetQuestionByIdAsync(int id, CancellationToken ct = default)
        => await _repository
            .GetByCondition<QuizQuestion>(q => q.Id == id, trackChanges: false)
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<List<QuizQuestion>> GetQuestionsForLessonAsync(int lessonId, CancellationToken ct = default)
        => await _repository
            .GetByCondition<QuizQuestion>(q => q.LessonId == lessonId, trackChanges: false)
            .OrderBy(q => q.SequenceOrder)
            .Take(500)
            .ToListAsync(ct);
}

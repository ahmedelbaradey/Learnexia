using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Learning;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Learning.Infrastructure.Contracts;

/// <summary>
/// Implements <see cref="IQuestionAnswerContract"/> by querying <see cref="LearningDbContext"/>
/// for the question's correct answer and the student's current hint level derived from the
/// attempt's <c>HintsUsedCount</c> (P3-05 OQ-2b + OQ-4).
///
/// <para>Cross-module isolation: the Ai module injects <see cref="IQuestionAnswerContract"/>
/// from <c>Shared.Contracts</c> — it never references this adapter or any Learning project
/// directly. This adapter lives in <c>Learning.Infrastructure</c> and is registered in
/// <c>AddLearningInfrastructure</c>.</para>
///
/// <para>CurrentHintLevel is derived as <c>Attempt.HintsUsedCount + 1</c>:
/// if the student has used 0 hints so far, the next (current) hint is level 1;
/// after one hint, level 2; etc. This is clamped to MaxHintLevels by the handler.</para>
/// </summary>
internal sealed class QuestionAnswerContractAdapter : IQuestionAnswerContract
{
    private readonly LearningDbContext _db;

    public QuestionAnswerContractAdapter(LearningDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<QuestionAnswerDto?> GetQuestionAnswerAsync(
        int questionId,
        int attemptId,
        int studentId,
        CancellationToken ct = default)
    {
        var question = await _db.Set<QuizQuestion>()
            .AsNoTracking()
            .Where(q => q.Id == questionId)
            .Select(q => new { q.CorrectAnswer })
            .FirstOrDefaultAsync(ct);

        if (question is null)
            return null;

        // Ownership guard (IDOR prevention): scope the attempt lookup by BOTH id AND the
        // authenticated student's id. Returns null when the attempt does not exist OR belongs
        // to a different student — the caller treats both cases identically ("not available").
        var attempt = await _db.Set<Attempt>()
            .AsNoTracking()
            .Where(a => a.Id == attemptId && a.StudentId == studentId)
            .Select(a => new { a.HintsUsedCount })
            .FirstOrDefaultAsync(ct);

        if (attempt is null)
            return null;

        // CurrentHintLevel is the NEXT hint the student would receive — level 1 on first request.
        var currentHintLevel = attempt.HintsUsedCount + 1;

        return new QuestionAnswerDto(
            CorrectAnswer: question.CorrectAnswer,
            CurrentHintLevel: currentHintLevel);
    }
}

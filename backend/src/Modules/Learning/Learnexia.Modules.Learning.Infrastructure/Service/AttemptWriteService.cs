using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

/// <summary>
/// Infrastructure implementation of <see cref="IAttemptWriteService"/> (Stage 6 — Option C).
///
/// Owns all EF queries and entity staging for the write-path attempt handlers:
/// AbandonAttemptCommandHandler, CompleteAttemptCommandHandler, and SubmitAnswerCommandHandler.
///
/// TRANSACTION DISCIPLINE — no SaveChangesAsync:
/// All write/upsert methods STAGE changes only. The UnitOfWorkBehavior commits atomically
/// after the handler returns. This preserves the invariant that the attempt status update,
/// mastery upserts, and SR field updates all commit in a SINGLE ambient transaction.
///
/// The sole exception is AttemptService.StartNewAsync (pre-existing escape hatch for
/// DB-generated Id — left as-is per Stage 6 constraint).
/// </summary>
public class AttemptWriteService : IAttemptWriteService
{
    private readonly ILearningRepository _repository;
    private readonly LearningDbContext _dbContext;
    private readonly ILoggerManager _logger;
    private readonly IOptions<SpacedRepetitionOptions> _srOptions;

    public AttemptWriteService(
        ILearningRepository repository,
        LearningDbContext dbContext,
        ILoggerManager logger,
        IOptions<SpacedRepetitionOptions> srOptions)
    {
        _repository = repository;
        _dbContext  = dbContext;
        _logger     = logger;
        _srOptions  = srOptions;
    }

    // ── Load operations ────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Attempt?> GetAttemptTrackedAsync(int attemptId, CancellationToken ct = default)
        => await _dbContext.Attempts
            .FirstOrDefaultAsync(a => a.Id == attemptId, ct);

    /// <inheritdoc/>
    public async Task<List<StudentAnswer>> GetAnswersForAttemptAsync(int attemptId, CancellationToken ct = default)
        => await _dbContext.StudentAnswers
            .AsNoTracking()
            .Where(sa => sa.AttemptId == attemptId)
            .ToListAsync(ct);

    // ── Stage operations ───────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task StageAttemptUpdateAsync(Attempt attempt, CancellationToken ct = default)
        => await _repository.UpdateAsync(attempt);

    /// <inheritdoc/>
    public async Task StageAddStudentAnswerAsync(StudentAnswer answer, CancellationToken ct = default)
        => await _repository.AddAsync(answer, ct);

    // ── Mastery + Spaced-Repetition upsert ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task UpsertMasteryAndAdvanceSRAsync(
        int studentId,
        int attemptId,
        IList<StudentAnswer> answers,
        CancellationToken ct = default)
    {
        if (answers.Count == 0)
            return;

        // ── Step A: Fetch the SkillId for each question in this attempt (batch, one query).
        // Questions with null SkillId are excluded (no skill attribution → no mastery update).
        var questionIds = answers.Select(a => a.QuestionId).Distinct().ToList();

        var questionSkillMap = await _dbContext.QuizQuestions
            .AsNoTracking()
            .Where(qq => questionIds.Contains(qq.Id) && qq.SkillId.HasValue)
            .Select(qq => new { qq.Id, SkillId = qq.SkillId!.Value })
            .ToListAsync(ct);

        if (questionSkillMap.Count == 0)
        {
            _logger.LogWarn($"P3-09: No skill-tagged questions found for studentId={studentId}; mastery not updated.");
            return;
        }

        var skillIdByQuestionId = questionSkillMap.ToDictionary(q => q.Id, q => q.SkillId);

        // ── Step B: Aggregate per-skill correct/total counts (in memory — small list).
        var perSkillAggregates = answers
            .Where(a => skillIdByQuestionId.ContainsKey(a.QuestionId))
            .GroupBy(a => skillIdByQuestionId[a.QuestionId])
            .Select(g => new { SkillId = g.Key, Total = g.Count(), Correct = g.Count(a => a.IsCorrect) })
            .ToList();

        var skillIds = perSkillAggregates.Select(a => a.SkillId).ToList();

        // ── Step C: Fetch existing mastery rows (no tracking) and per-skill MasteryThreshold.
        var existingRows = await _repository.GetSkillMasteryRowsAsync(studentId, skillIds.AsReadOnly(), ct);
        var existingBySkillId = existingRows.ToDictionary(m => m.SkillId);

        var skills = await _dbContext.Skills
            .AsNoTracking()
            .Where(s => skillIds.Contains(s.Id))
            .Select(s => new { s.Id, s.MasteryThreshold })
            .ToListAsync(ct);
        var thresholdBySkillId = skills.ToDictionary(s => s.Id, s => s.MasteryThreshold);

        // ── Step D: For each skill, compute mastery and upsert (stage only).
        var now = DateTime.UtcNow;
        foreach (var agg in perSkillAggregates)
        {
            // Threshold: use per-skill value if available; fall back to 80 (FR-AD-3 default).
            var threshold = thresholdBySkillId.TryGetValue(agg.SkillId, out var t) ? t : 80;

            var (masteryPercentage, status) = MasteryEngine.Compute(agg.Total, agg.Correct, threshold);

            if (existingBySkillId.TryGetValue(agg.SkillId, out var existing))
            {
                // Update path: carry forward SR columns (P3-10) unchanged.
                var updated = new StudentSkillMastery
                {
                    Id                 = existing.Id,
                    StudentId          = studentId,
                    SkillId            = agg.SkillId,
                    MasteryPercentage  = masteryPercentage,
                    Status             = status,
                    AttemptsCount      = existing.AttemptsCount + 1,
                    LastPracticedAt    = now,
                    ReviewIntervalDays = existing.ReviewIntervalDays,
                    NextReviewDueAt    = existing.NextReviewDueAt,
                    RepetitionNumber   = existing.RepetitionNumber,
                };
                await _repository.UpsertStudentSkillMasteryAsync(updated, ct);
            }
            else
            {
                // Insert path: SR columns default to 0 / null per entity defaults (P3-10 reserved).
                var inserted = new StudentSkillMastery
                {
                    StudentId          = studentId,
                    SkillId            = agg.SkillId,
                    MasteryPercentage  = masteryPercentage,
                    Status             = status,
                    AttemptsCount      = 1,
                    LastPracticedAt    = now,
                };
                await _repository.UpsertStudentSkillMasteryAsync(inserted, ct);
            }
        }

        // ── Step E (P3-10): Interval-progression hook (spaced-repetition scheduler).
        // For each skill that had a PRE-upsert row (existingBySkillId), evaluate IsDue.
        // If due: advance (or reset) the ladder and write SR fields back inside the ambient UoW.
        // If not due (routine practice): skip.
        await AdvanceSpacedRepetitionAsync(studentId, answers, existingBySkillId, skillIdByQuestionId, ct);
    }

    // ── Submit-Answer helpers ──────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<QuizQuestion?> GetQuestionForAnswerAsync(int questionId, int lessonId, CancellationToken ct = default)
        => await _dbContext.QuizQuestions
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == questionId && q.LessonId == lessonId, ct);

    /// <inheritdoc/>
    public async Task<bool> IsAlreadyAnsweredAsync(int attemptId, int questionId, CancellationToken ct = default)
        => await _dbContext.StudentAnswers
            .AsNoTracking()
            .AnyAsync(sa => sa.AttemptId == attemptId && sa.QuestionId == questionId, ct);

    // ── Private helper: P3-10 Spaced-Repetition interval progression ──────────────────────────

    /// <summary>
    /// P3-10 — Interval-progression completion hook.
    ///
    /// For each distinct skill touched by this attempt, checks whether the skill was DUE for
    /// spaced-repetition review BEFORE this attempt (using the PRE-upsert mastery state).
    /// If due: calls <see cref="SpacedRepetitionEngine.ComputeNextReview"/> to advance (or reset)
    /// the ladder and writes the updated SR fields back inside the ambient UoW transaction.
    /// If not due (routine practice): leaves SR fields unchanged — skip.
    ///
    /// Uses the PRE-upsert rows (existingBySkillId) so IsDue is evaluated against the
    /// PRE-attempt mastery state. Post-upsert tracked rows are fetched to get the NEW Status
    /// for the "improved" flag, while keeping SR columns from the pre-upsert state.
    /// </summary>
    private async Task AdvanceSpacedRepetitionAsync(
        int studentId,
        IList<StudentAnswer> answers,
        Dictionary<int, StudentSkillMastery> preUpsertMasteryRows,
        Dictionary<int, int> skillIdByQuestionId,
        CancellationToken ct)
    {
        if (answers.Count == 0 || preUpsertMasteryRows.Count == 0)
            return;

        var options = _srOptions.Value;
        var utcNow  = DateTime.UtcNow;

        var skillIds = answers
            .Where(a => skillIdByQuestionId.ContainsKey(a.QuestionId))
            .Select(a => skillIdByQuestionId[a.QuestionId])
            .Distinct()
            .ToList();

        if (skillIds.Count == 0)
            return;

        // Fetch the POST-upsert tracked entities — their Status reflects the NEW mastery value
        // (used for the "improved" flag). SR columns are unchanged by the upsert.
        var postUpsertRows = await _dbContext.StudentSkillMasteries
            .Where(m => m.StudentId == studentId && skillIds.Contains(m.SkillId))
            .ToListAsync(ct);

        foreach (var postRow in postUpsertRows)
        {
            // Only process skills that had a pre-attempt row (new inserts have no SR history).
            if (!preUpsertMasteryRows.TryGetValue(postRow.SkillId, out var preRow))
                continue;

            // Evaluate IsDue against the PRE-attempt state (old Status + old NextReviewDueAt).
            var wasDue = SpacedRepetitionEngine.IsDue(preRow.Status, preRow.NextReviewDueAt, utcNow);

            if (!wasDue)
                continue; // routine practice — skip SR advancement

            // "improved" = the NEW mastery status (post-upsert) is not NeedsReview.
            var improved = postRow.Status != MasteryStatus.NeedsReview;

            var (nextIntervalDays, nextRepetitionNumber) =
                SpacedRepetitionEngine.ComputeNextReview(preRow.RepetitionNumber, improved, options);

            // Write SR fields back to the post-upsert tracked entity — UoW commits atomically.
            postRow.ReviewIntervalDays = nextIntervalDays;
            postRow.RepetitionNumber   = nextRepetitionNumber;
            postRow.NextReviewDueAt    = utcNow.AddDays(nextIntervalDays);

            // Stage the update (entity is already tracked; EF detects property changes).
            await _repository.UpdateAsync(postRow);
        }
    }
}

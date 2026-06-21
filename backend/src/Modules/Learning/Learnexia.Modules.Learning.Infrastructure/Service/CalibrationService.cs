using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Services;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Learnexia.Modules.Learning.Infrastructure.Persistence;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

/// <summary>
/// In-process implementation of <see cref="ICalibrationService"/> (P5-07 BE-1/BE-2/BE-3/BE-5).
///
/// OPTION C — EF only here (in Infrastructure), never in Application. Handlers and jobs inject
/// <see cref="ICalibrationService"/> from Application; only this class touches the DbContext.
///
/// DE-IDENTIFICATION (AC5 — HARD GATE):
///   <see cref="AggregateQuestionStatsAsync"/> groups <c>StudentAnswer</c> by <c>QuestionId</c>
///   and projects <b>counts/aggregates ONLY</b>:
///     SampleSize = COUNT(*), PValue = AVG(IsCorrect), AvgTimeSpentSeconds = AVG(TimeSpentSeconds),
///     HintUsageRate = AVG(HintUsed).
///   <b>No StudentId, AttemptId, or any child identifier is selected, persisted, or logged.</b>
///   The query never navigates to Attempt.StudentId — it groups directly on StudentAnswer by QuestionId.
///
/// TRANSACTION NOTE:
///   (a) Job call site: aggregation / proposals / flags are staged; the job calls
///       LearningDbContext.SaveChangesAsync(userId: 0) per-question after each step (ADR-0001).
///   (b) BE-5 command handlers: run inside UnitOfWorkBehavior — the UoW commits after the handler.
///
/// AUTHORED-CONTENT RULE: this class NEVER mutates QuizQuestion.Difficulty, QuestionText, Options,
/// or CorrectAnswer. The only permitted mutation is QualityState + FlagReason (system flag,
/// reversible). The authored band changes exclusively via the human Promote path (BE-5).
/// </summary>
public sealed class CalibrationService : ICalibrationService
{
    private readonly LearningDbContext _dbContext;
    private readonly CalibrationOptions _options;

    public CalibrationService(LearningDbContext dbContext, IOptions<CalibrationOptions> options)
    {
        _dbContext = dbContext;
        _options   = options.Value;
    }

    // ── BE-1: aggregation ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<int> AggregateQuestionStatsAsync(CancellationToken ct = default)
    {
        // ── Aggregation query — AC5 HARD GATE ─────────────────────────────────────────────────────
        // Group StudentAnswers by QuestionId ONLY.
        // Projects counts/averages ONLY — no StudentId, AttemptId, or child identifier selected.
        // AsNoTracking — read-only aggregate pass.
        var aggregates = await _dbContext.StudentAnswers
            .AsNoTracking()
            .GroupBy(sa => sa.QuestionId)
            .Select(g => new
            {
                QuestionId          = g.Key,
                SampleSize          = g.Count(),
                PValue              = g.Average(sa => sa.IsCorrect ? 1.0 : 0.0),
                AvgTimeSpentSeconds = g.Average(sa => (double)sa.TimeSpentSeconds),
                HintUsageRate       = g.Average(sa => sa.HintUsed ? 1.0 : 0.0),
            })
            .ToListAsync(ct);

        var now = DateTime.UtcNow;

        foreach (var agg in aggregates)
        {
            // Upsert: lookup existing row by QuestionId (tracked).
            var existing = await _dbContext.QuestionDifficultyStats
                .FirstOrDefaultAsync(s => s.QuestionId == agg.QuestionId, ct);

            if (existing is null)
            {
                // Insert path — stage the new row; caller commits.
                await _dbContext.QuestionDifficultyStats.AddAsync(new QuestionDifficultyStats
                {
                    QuestionId          = agg.QuestionId,
                    SampleSize          = agg.SampleSize,
                    PValue              = agg.PValue,
                    AvgTimeSpentSeconds = agg.AvgTimeSpentSeconds,
                    HintUsageRate       = agg.HintUsageRate,
                    ComputedAtUtc       = now,
                }, ct);
            }
            else
            {
                // Update path — mutate in-place (idempotent upsert).
                existing.SampleSize          = agg.SampleSize;
                existing.PValue              = agg.PValue;
                existing.AvgTimeSpentSeconds = agg.AvgTimeSpentSeconds;
                existing.HintUsageRate       = agg.HintUsageRate;
                existing.ComputedAtUtc       = now;

                _dbContext.QuestionDifficultyStats.Update(existing);
            }
        }

        return aggregates.Count;
    }

    // ── BE-2: divergence proposals ────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<int> UpsertDivergenceProposalsAsync(CancellationToken ct = default)
    {
        // Load all stats rows + their owning question's Difficulty (authored band).
        // AsNoTracking for the read pass — we upsert proposals separately with tracking.
        var statsRows = await _dbContext.QuestionDifficultyStats
            .AsNoTracking()
            .Include(s => s.Question)
            .ToListAsync(ct);

        int proposalCount = 0;

        foreach (var stats in statsRows)
        {
            if (stats.Question is null)
                continue;

            var decision = CalibrationEngine.EvaluateDivergence(
                stats, stats.Question.Difficulty, _options);

            if (!decision.ShouldPropose)
                continue;

            // Upsert open Pending proposal for this question.
            // The partial unique index (WHERE Status=1) guarantees at most one Pending row per
            // question at the DB level; we upsert in-logic to avoid a duplicate-key violation.
            var existing = await _dbContext.QuestionRecalibrationProposals
                .FirstOrDefaultAsync(
                    p => p.QuestionId == stats.QuestionId && p.Status == ProposalStatus.Pending,
                    ct);

            if (existing is null)
            {
                // Insert path: new Pending proposal.
                await _dbContext.QuestionRecalibrationProposals.AddAsync(
                    new QuestionRecalibrationProposal
                    {
                        QuestionId          = stats.QuestionId,
                        AuthoredDifficulty  = stats.Question.Difficulty,
                        ProposedDifficulty  = decision.EmpiricalBand,
                        EvidencePValue      = stats.PValue,
                        EvidenceSampleSize  = stats.SampleSize,
                        ReasonCode          = decision.ReasonCode,
                        Status              = ProposalStatus.Pending,
                    }, ct);
            }
            else
            {
                // Update path: refresh evidence on the open proposal (re-run idempotency).
                // AuthoredDifficulty snapshot is NOT updated (preserves the before-picture per AC7).
                existing.ProposedDifficulty = decision.EmpiricalBand;
                existing.EvidencePValue     = stats.PValue;
                existing.EvidenceSampleSize = stats.SampleSize;
                existing.ReasonCode         = decision.ReasonCode;

                _dbContext.QuestionRecalibrationProposals.Update(existing);
            }

            proposalCount++;
        }

        return proposalCount;
    }

    // ── BE-3: AI quality flags ────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<int> ApplyAiFlagsAsync(CancellationToken ct = default)
    {
        // Load stats rows with their owning question (tracked — we mutate QualityState / FlagReason).
        var statsRows = await _dbContext.QuestionDifficultyStats
            .AsNoTracking()
            .Include(s => s.Question)
            .ToListAsync(ct);

        int changedCount = 0;

        foreach (var stats in statsRows)
        {
            if (stats.Question is null)
                continue;

            // Reload the question with tracking so EF can manage the update.
            var question = await _dbContext.QuizQuestions
                .FirstOrDefaultAsync(q => q.Id == stats.QuestionId, ct);

            if (question is null)
                continue;

            var flagDecision = CalibrationEngine.EvaluateAiFlag(stats, question.GeneratedBy, _options);

            if (flagDecision.ShouldFlag)
            {
                // Only count a change if the flag state actually changes.
                if (question.QualityState != QuestionQualityState.FlaggedForReview
                    || question.FlagReason != flagDecision.ReasonCode)
                {
                    question.QualityState = QuestionQualityState.FlaggedForReview;
                    question.FlagReason   = flagDecision.ReasonCode;
                    _dbContext.QuizQuestions.Update(question);
                    changedCount++;
                }
            }
            else
            {
                // Clear the flag if it was previously set (reversible system flag).
                if (question.QualityState == QuestionQualityState.FlaggedForReview)
                {
                    question.QualityState = QuestionQualityState.Approved;
                    question.FlagReason   = null;
                    _dbContext.QuizQuestions.Update(question);
                    changedCount++;
                }
            }
        }

        return changedCount;
    }

    // ── BE-5: admin query + command helpers ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<PaginatedResult<QuestionRecalibrationProposal>> GetProposalsPagedAsync(
        int? statusFilter, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _dbContext.QuestionRecalibrationProposals
            .AsNoTracking()
            .Include(p => p.Question)
            .AsQueryable();

        if (statusFilter.HasValue)
            query = query.Where(p => (int)p.Status == statusFilter.Value);

        query = query.OrderByDescending(p => p.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PaginatedResult<QuestionRecalibrationProposal>.Success(items, totalCount, page, pageSize);
    }

    /// <inheritdoc/>
    public async Task<PaginatedResult<FlaggedQuestionRow>> GetFlaggedQuestionsPagedAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        // Fetch flagged questions (all, including AI and curated — admin sees all).
        var query = _dbContext.QuizQuestions
            .AsNoTracking()
            .Where(q => q.QualityState == QuestionQualityState.FlaggedForReview)
            .OrderByDescending(q => q.Id);

        var totalCount = await query.CountAsync(ct);
        var questions  = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Batch-load the matching stats rows so the DTO can carry the evidence p-value.
        var questionIds = questions.Select(q => q.Id).ToHashSet();
        var statsById = await _dbContext.QuestionDifficultyStats
            .AsNoTracking()
            .Where(s => questionIds.Contains(s.QuestionId))
            .ToDictionaryAsync(s => s.QuestionId, ct);

        var rows = questions
            .Select(q => new FlaggedQuestionRow(
                Question: q,
                Stats:    statsById.GetValueOrDefault(q.Id)))
            .ToList();

        return PaginatedResult<FlaggedQuestionRow>.Success(rows, totalCount, page, pageSize);
    }

    /// <inheritdoc/>
    public async Task<QuestionRecalibrationProposal?> GetProposalTrackedAsync(int proposalId, CancellationToken ct = default)
        => await _dbContext.QuestionRecalibrationProposals
            .Include(p => p.Question)
            .FirstOrDefaultAsync(p => p.Id == proposalId, ct);

    /// <inheritdoc/>
    public async Task<QuizQuestion?> GetQuestionTrackedAsync(int questionId, CancellationToken ct = default)
        => await _dbContext.QuizQuestions
            .FirstOrDefaultAsync(q => q.Id == questionId, ct);
}

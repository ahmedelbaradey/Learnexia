using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

/// <summary>
/// Infrastructure implementation of <see cref="IAttemptQueryService"/> (Stage 6 — Option C).
///
/// Encapsulates all EF reads for GetStudentAttemptsQueryHandler and GetSkillStatsQueryHandler.
/// All results are fully materialized — no IQueryable crosses the service boundary.
/// No SaveChangesAsync — read-only service.
/// </summary>
public class AttemptQueryService : IAttemptQueryService
{
    private readonly LearningDbContext _dbContext;

    public AttemptQueryService(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<List<Attempt>> GetAttemptsForStudentAsync(int studentId, CancellationToken ct = default)
        => await _dbContext.Attempts
            .AsNoTracking()
            .Where(a => a.StudentId == studentId)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<SkillStatsDto> GetSkillStatsAsync(int studentId, int skillId, CancellationToken ct = default)
    {
        // Mirrors GetSkillStatsQueryHandler step 3: JOIN via navigation properties.
        // QuizQuestion.SkillId == skillId equality excludes null (correct behaviour).
        var answers = await _dbContext.StudentAnswers
            .AsNoTracking()
            .Where(sa => sa.Question.SkillId == skillId
                      && sa.Attempt.StudentId == studentId)
            .Select(sa => new { sa.IsCorrect, sa.TimeSpentSeconds, sa.HintUsed })
            .ToListAsync(ct);

        var total   = answers.Count;
        var correct = answers.Count(a => a.IsCorrect);

        return new SkillStatsDto
        {
            SkillId             = skillId,
            StudentId           = studentId,
            TotalAnswers        = total,
            CorrectAnswers      = correct,
            AccuracyPercentage  = total == 0 ? 0.0 : Math.Round((double)correct / total * 100, 2),
            AvgTimeSpentSeconds = total == 0 ? 0.0 : Math.Round(answers.Average(a => a.TimeSpentSeconds), 2),
            HintUsageRate       = total == 0 ? 0.0 : Math.Round(answers.Count(a => a.HintUsed) / (double)total * 100, 2),
        };
    }
}

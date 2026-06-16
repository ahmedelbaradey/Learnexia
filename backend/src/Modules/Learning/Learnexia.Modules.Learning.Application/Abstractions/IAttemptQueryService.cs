using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;

namespace Learnexia.Modules.Learning.Application.Abstractions;

/// <summary>
/// Service seam for attempt-read queries used by GetStudentAttemptsQueryHandler and
/// GetSkillStatsQueryHandler (Stage 6 — Option C refactor).
///
/// All EF queries are encapsulated here — Application query handlers inject this interface only.
/// No IQueryable / DbSet / EF extension methods cross this boundary.
/// Results are always materialized (List / SkillStatsDto).
/// </summary>
public interface IAttemptQueryService
{
    /// <summary>
    /// Returns all <see cref="Attempt"/> rows for the given student, ordered by
    /// StartedAt descending (most recent first). Returns an empty list when no rows exist.
    /// AsNoTracking.
    /// </summary>
    Task<List<Attempt>> GetAttemptsForStudentAsync(int studentId, CancellationToken ct = default);

    /// <summary>
    /// Aggregates <see cref="StudentAnswer"/> rows for the given (skillId, studentId) pair
    /// (via JOIN on QuizQuestion.SkillId and Attempt.StudentId) and returns a
    /// <see cref="SkillStatsDto"/> with TotalAnswers, CorrectAnswers, AccuracyPercentage,
    /// AvgTimeSpentSeconds, and HintUsageRate populated.
    ///
    /// Zero-data case (no matching answers) returns a zeroed DTO — never throws / never 404.
    /// AsNoTracking.
    /// </summary>
    Task<SkillStatsDto> GetSkillStatsAsync(int studentId, int skillId, CancellationToken ct = default);
}

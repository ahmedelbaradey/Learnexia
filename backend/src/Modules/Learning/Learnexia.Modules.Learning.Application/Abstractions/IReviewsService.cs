using Learnexia.Modules.Learning.Domain.Entities;

namespace Learnexia.Modules.Learning.Application.Abstractions;

/// <summary>
/// Service seam for spaced-repetition review reads (P3-10).
/// Encapsulates the EF query for due mastery rows so that Application query handlers
/// remain EF-free (Option C). No IQueryable / DbSet / EF extension methods cross this boundary.
/// </summary>
public interface IReviewsService
{
    /// <summary>
    /// Returns all <see cref="StudentSkillMastery"/> rows that are due for spaced-repetition
    /// review at <paramref name="utcNow"/>, with the Skill navigation populated (for Skill.Name).
    /// Applies the IsDue rule (NeedsReview OR Mastered+stale). AsNoTracking.
    /// UTC discipline: caller must pass DateTime.UtcNow.
    /// </summary>
    Task<IReadOnlyList<StudentSkillMastery>> GetDueMasteryRowsAsync(
        DateTime utcNow, CancellationToken ct = default);
}

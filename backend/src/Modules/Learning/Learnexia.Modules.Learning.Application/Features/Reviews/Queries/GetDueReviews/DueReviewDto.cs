using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.Reviews.Queries.GetDueReviews;

/// <summary>
/// DTO returned by <see cref="GetDueReviewsQuery"/> for each skill due for spaced-repetition review.
/// </summary>
public sealed class DueReviewDto
{
    /// <summary>Intra-Learning skill identifier.</summary>
    public int SkillId { get; set; }

    /// <summary>Display name of the skill (from the <c>Skill</c> entity).</summary>
    public string SkillName { get; set; } = string.Empty;

    /// <summary>
    /// UTC datetime when this review became (or will become) due.
    /// Null if the skill is in <c>NeedsReview</c> status and no interval has been scheduled yet.
    /// </summary>
    public DateTime? NextReviewDueAt { get; set; }

    /// <summary>Current mastery status — <c>NeedsReview</c> or <c>Mastered</c> (stale).</summary>
    public MasteryStatus Status { get; set; }

    /// <summary>
    /// Current ladder position (0-based index into <c>IntervalLadderDays</c>).
    /// 0 = shortest interval (never successfully reviewed or after a reset).
    /// </summary>
    public int RepetitionNumber { get; set; }
}

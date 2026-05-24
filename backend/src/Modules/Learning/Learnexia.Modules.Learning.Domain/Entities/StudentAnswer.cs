using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Learning.Domain.Entities;

/// <summary>
/// Records a student's answer to a single question within an attempt.
/// Child of the Attempt aggregate (not its own AggregateRoot).
///
/// AttemptId carries a real intra-module FK (cascade delete — deleting an Attempt removes its answers).
/// QuestionId carries a real intra-module FK (Restrict — cannot delete a question while answers exist).
///
/// IsCorrect, HintUsed — populated by P2-07 (instant feedback).
/// TimeSpentSeconds — populated by P2-08 (granular timing).
/// AnswerPayload holds the serialized student-supplied answer value.
/// </summary>
public class StudentAnswer : FullAuditedEntity
{
    public int AttemptId { get; set; }
    public Attempt Attempt { get; set; } = null!;

    public int QuestionId { get; set; }
    public QuizQuestion Question { get; set; } = null!;

    /// <summary>Serialized student-supplied answer; shape matches the QuizQuestion.QuestionType.</summary>
    public string AnswerPayload { get; set; } = null!;

    /// <summary>Whether the answer was correct; populated by P2-07.</summary>
    public bool IsCorrect { get; set; }

    /// <summary>Seconds spent on this question; populated by P2-08.</summary>
    public int TimeSpentSeconds { get; set; }

    /// <summary>Whether a hint was used for this question; populated by P2-07/P2-08.</summary>
    public bool HintUsed { get; set; }
}

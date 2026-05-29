namespace Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;

/// <summary>
/// Summary DTO returned by CompleteAttemptCommand and AbandonAttemptCommand.
/// Status is a string ("Completed" or "Abandoned") — stringified from AttemptStatus enum in the handler.
/// TotalAnswers and CorrectAnswers are filled explicitly by the handler after aggregate recompute;
/// AutoMapper ignores those two members (see QuizProfile).
/// </summary>
public class AttemptSummaryDto
{
    public int AttemptId { get; set; }
    public int StudentId { get; set; }
    public int LessonId { get; set; }

    /// <summary>"Completed" or "Abandoned" — stringified from AttemptStatus enum, never a raw int.</summary>
    public string Status { get; set; } = null!;

    public double AccuracyPercentage { get; set; }
    public int DurationSeconds { get; set; }
    public int HintsUsedCount { get; set; }
    public DateTime StartedAt { get; set; }

    /// <summary>Null until the attempt reaches a terminal state.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Total number of answers submitted in this attempt. Filled by the handler, not AutoMapper.</summary>
    public int TotalAnswers { get; set; }

    /// <summary>Number of correct answers. Filled by the handler, not AutoMapper.</summary>
    public int CorrectAnswers { get; set; }
}

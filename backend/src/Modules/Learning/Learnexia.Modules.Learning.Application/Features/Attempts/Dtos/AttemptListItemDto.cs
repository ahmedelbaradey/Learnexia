namespace Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;

/// <summary>
/// DTO returned by GetStudentAttemptsQuery — a summary of one attempt.
/// SECURITY: CorrectAnswer is intentionally absent; this DTO must NEVER contain it.
/// </summary>
public class AttemptListItemDto
{
    public int Id { get; set; }
    public int LessonId { get; set; }
    public string Status { get; set; } = null!;
    public double AccuracyPercentage { get; set; }
    public int DurationSeconds { get; set; }
    public int HintsUsedCount { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

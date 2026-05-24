namespace Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;

/// <summary>
/// Response returned when a student starts a quiz attempt.
/// Contains the new AttemptId and the lesson's questions WITHOUT their correct answers
/// (CorrectAnswer is never sent to the client; it is checked server-side in P2-07).
/// </summary>
public class StartAttemptResponse
{
    public int AttemptId { get; set; }

    /// <summary>
    /// Questions for this lesson. Empty list when the lesson has no questions yet.
    /// CorrectAnswer is intentionally absent from each QuizQuestionDto.
    /// </summary>
    public List<QuizQuestionDto> Questions { get; set; } = new();
}

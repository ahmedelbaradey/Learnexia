namespace Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;

/// <summary>
/// Response returned after a student submits an answer.
/// IsCorrect is always populated. CorrectAnswer is null when the answer is correct (the client
/// already knows the answer) and populated only when the answer is wrong.
/// HintAvailable is a stub (false) for P2-08; P2-07 will wire it to the hint logic.
/// </summary>
public class SubmitAnswerResponse
{
    public bool IsCorrect { get; set; }

    /// <summary>
    /// The correct answer value — returned only when IsCorrect=false so the client
    /// never receives it for free. Null when the student answered correctly.
    /// </summary>
    public string? CorrectAnswer { get; set; }

    /// <summary>
    /// Whether a hint is available for this question. Stub = false for P2-08;
    /// P2-07 will wire this to the hint availability logic.
    /// </summary>
    public bool HintAvailable { get; set; }
}

using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;

/// <summary>
/// Question DTO sent to the client when starting an attempt.
/// CorrectAnswer is intentionally EXCLUDED — the answer is checked server-side (P2-07).
/// </summary>
public class QuizQuestionDto
{
    public int Id { get; set; }
    public QuestionType QuestionType { get; set; }
    public string QuestionText { get; set; } = null!;

    /// <summary>
    /// Serialized options (jsonb). Shape varies per QuestionType.
    /// CorrectAnswer is NOT included in this DTO.
    /// </summary>
    public string Options { get; set; } = null!;
}

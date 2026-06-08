using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.Questions.Dtos;

/// <summary>
/// Input shape for editing an existing quiz question.
/// IsActive, IsDeleted, and SequenceOrder are intentionally absent (mass-assignment guard):
///   - IsActive is toggled via SetQuestionActiveCommand.
///   - SequenceOrder is updated via ReorderQuestionsCommand.
///   - IsDeleted is set via DeleteQuestionCommand.
/// </summary>
public record EditQuestionDto
{
    public int Id { get; init; }
    public QuestionType QuestionType { get; init; }

    /// <summary>Question text. Max 4096 chars (DoS guard — jsonb col is unbounded at DB level).</summary>
    public string QuestionText { get; init; } = null!;

    /// <summary>Serialized options JSON. Per-type shape validated by QuizQuestionTypeValidation. Max 16384 chars.</summary>
    public string Options { get; init; } = null!;

    /// <summary>Serialized correct answer JSON. Per-type shape validated by QuizQuestionTypeValidation. Max 4096 chars.</summary>
    public string CorrectAnswer { get; init; } = null!;

    public DifficultyLevel Difficulty { get; init; }
}

using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.Questions.Dtos;

/// <summary>
/// Input shape for adding a new quiz question to a lesson.
/// IsActive, IsDeleted, and SequenceOrder are intentionally absent (mass-assignment guard):
///   - IsActive defaults to <c>true</c> on the entity.
///   - SequenceOrder is set by the handler (append: max + 1).
///   - IsDeleted is never set on Add.
/// </summary>
public record AddQuestionDto
{
    public int LessonId { get; init; }
    public int? SkillId { get; init; }
    public QuestionType QuestionType { get; init; }

    /// <summary>Question text. Max 4096 chars (DoS guard — jsonb col is unbounded at DB level).</summary>
    public string QuestionText { get; init; } = null!;

    /// <summary>Serialized options JSON. Per-type shape validated by QuizQuestionTypeValidation. Max 16384 chars.</summary>
    public string Options { get; init; } = null!;

    /// <summary>Serialized correct answer JSON. Per-type shape validated by QuizQuestionTypeValidation. Max 4096 chars.</summary>
    public string CorrectAnswer { get; init; } = null!;

    public DifficultyLevel Difficulty { get; init; }
    public GeneratedBy GeneratedBy { get; init; }
}

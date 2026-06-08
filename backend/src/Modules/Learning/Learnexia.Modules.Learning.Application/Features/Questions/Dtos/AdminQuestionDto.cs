using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.Questions.Dtos;

/// <summary>
/// Admin-facing quiz question DTO — includes <c>CorrectAnswer</c> and <c>IsActive</c>
/// for authoring purposes.
///
/// SECURITY: This DTO is returned ONLY from AdminOnly-gated endpoints in <c>QuestionsController</c>.
/// It must never be returned from student-facing routes. The student-facing DTO is
/// <see cref="Learnexia.Modules.Learning.Application.Features.Attempts.Dtos.QuizQuestionDto"/>
/// which deliberately excludes <c>CorrectAnswer</c>.
/// </summary>
public record AdminQuestionDto
{
    public int Id { get; init; }
    public int LessonId { get; init; }
    public int? SkillId { get; init; }
    public QuestionType QuestionType { get; init; }
    public string QuestionText { get; init; } = null!;

    /// <summary>Serialized options JSON.</summary>
    public string Options { get; init; } = null!;

    /// <summary>
    /// Serialized correct answer JSON.
    /// SECURITY: Intentionally included here for admin authoring only.
    /// The student-facing QuizQuestionDto excludes this field.
    /// </summary>
    public string CorrectAnswer { get; init; } = null!;

    public DifficultyLevel Difficulty { get; init; }
    public GeneratedBy GeneratedBy { get; init; }
    public int SequenceOrder { get; init; }

    /// <summary>Admin-controlled active/inactive toggle. Inactive questions are hidden from students.</summary>
    public bool IsActive { get; init; }
}

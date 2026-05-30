using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Dtos;

/// <summary>
/// Full lesson assembly response for the student-facing lesson screen (P2-05).
/// Extends <see cref="LessonDto"/> with static content fields and an embedded quick-check question.
///
/// SECURITY: <see cref="QuickCheck"/> uses the existing <c>QuizQuestionDto</c> which intentionally
/// omits <c>CorrectAnswer</c> via <c>QuizProfile</c>. Do NOT add a CorrectAnswer field here.
/// Cross-feature DTO reuse is intentional — same Application project (Learning.Application),
/// no architectural rule violated.
/// </summary>
public record SingleLessonResponse : LessonDto
{
    /// <summary>
    /// Optional static/seeded explanation (Markdown). Null when not yet authored.
    /// Phase 3 will swap the source to the AI tutor (P3-04) — column shape is neutral.
    /// </summary>
    public string? Explanation { get; set; }

    /// <summary>
    /// Optional visual example — a URL or short asset key. Rendered by the FE.
    /// Null when not yet authored.
    /// </summary>
    public string? Visual { get; set; }

    /// <summary>
    /// True when this lesson is the end-of-unit boss/challenge (FR-LR-2 fourth node category).
    /// Orthogonal to <see cref="NodeState"/> — a boss can be Locked, Available, or Completed.
    /// </summary>
    public bool IsBoss { get; set; }

    /// <summary>
    /// First <see cref="QuizQuestionDto"/> for this lesson by Id ASC. Null if the lesson
    /// has no quiz questions yet. <c>CorrectAnswer</c> is never included in this DTO.
    /// </summary>
    public QuizQuestionDto? QuickCheck { get; set; }
}

using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Learning.Domain.Entities;

/// <summary>
/// A quiz question supporting four types (MCQ, TrueFalse, Matching, FillInBlank).
/// Options and CorrectAnswer are stored as jsonb (serialized) — per-type shape is enforced in
/// FluentValidation, not the schema (single-entity + enum discriminator, no TPH/inheritance).
///
/// LessonId and SkillId are plain int loose references (module isolation within Learning;
/// kept as plain int + index rather than a real FK so quiz lifecycle is decoupled from lesson deletes).
/// </summary>
public class QuizQuestion : AggregateRoot
{
    /// <summary>Loose reference to Learning.Lesson (plain int, index only — decouples quiz lifecycle from lesson deletes).</summary>
    public int LessonId { get; set; }

    /// <summary>Loose reference to Learning.Skill (plain int?, index only).</summary>
    public int? SkillId { get; set; }

    public QuestionType QuestionType { get; set; }

    public string QuestionText { get; set; } = null!;

    /// <summary>
    /// Serialized options (jsonb column). Shape varies per QuestionType:
    /// MCQ — array of strings; TrueFalse — fixed ["True","False"];
    /// Matching — object {"left":[...],"right":[...]}; FillInBlank — not used.
    /// Per-type shape is enforced in FluentValidation only.
    /// </summary>
    public string Options { get; set; } = null!;

    /// <summary>
    /// Serialized correct answer (jsonb column). Shape varies per QuestionType.
    /// Never exposed in the client DTO — excluded by mapping profile.
    /// </summary>
    public string CorrectAnswer { get; set; } = null!;

    public DifficultyLevel Difficulty { get; set; }

    public GeneratedBy GeneratedBy { get; set; }

    public ICollection<StudentAnswer> StudentAnswers { get; set; } = new List<StudentAnswer>();
}

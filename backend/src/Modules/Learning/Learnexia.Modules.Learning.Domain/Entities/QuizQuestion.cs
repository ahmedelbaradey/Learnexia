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
///
/// P7-04: Added <see cref="IsActive"/> (admin-controlled active/inactive toggle) and
/// <see cref="SequenceOrder"/> (display order within the owning lesson/skill question set).
/// Soft-delete (<c>IsDeleted</c> + <c>DeletedAt</c>) is inherited from <see cref="AggregateRoot"/>
/// via <c>FullAuditedEntity</c>.
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
    /// Matching — object {"left":[{"id","text"}],"right":[{"id","text"}]} with stable item ids and
    /// localized display text (CO-BE-1 contract; store "right" in non-aligned order so array
    /// position never leaks the pairing); FillInBlank — not used (stored as []).
    /// Per-type shape is enforced in FluentValidation only.
    /// </summary>
    public string Options { get; set; } = null!;

    /// <summary>
    /// Serialized correct answer (jsonb column). Shape varies per QuestionType:
    /// MCQ/FillInBlank — JSON string; TrueFalse — "true"/"false";
    /// Matching — {"pairs":[{"leftId","rightId"}]} referencing the Options item ids
    /// (CO-BE-1 contract; comparator semantics documented on AnswerComparator).
    /// Never exposed in the student-facing DTO — excluded by mapping profile.
    /// Exposed in the admin authoring DTO (distinct type, AdminOnly-gated).
    /// </summary>
    public string CorrectAnswer { get; set; } = null!;

    public DifficultyLevel Difficulty { get; set; }

    public GeneratedBy GeneratedBy { get; set; }

    /// <summary>
    /// P7-04: Display order within the owning lesson's question set (0-based).
    /// Composite index <c>(LessonId, SequenceOrder)</c> is defined in <c>QuizQuestionConfig</c>.
    /// Used by <c>ReorderQuestionsCommand</c> (batch, explicit transaction).
    /// Default 0 (backfill-safe for existing seeded rows).
    /// </summary>
    public int SequenceOrder { get; set; }

    /// <summary>
    /// P7-04: Admin-controlled active/inactive toggle. Inactive questions are hidden from
    /// student-facing quiz reads but are NOT deleted (use <c>IsDeleted</c> for soft-delete).
    /// Default <c>true</c>. Mirrors Subject/Unit/Lesson/ContentBlock pattern.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// P7-05: Editorial lifecycle state (Draft / Published / Archived).
    /// DB column defaults to 2 (Published) — all existing rows backfill to Published.
    /// C# initializer is Draft — new instances start as Draft.
    /// Stored as int via HasConversion&lt;int&gt;() in QuizQuestionConfig.
    /// </summary>
    public LifecycleState LifecycleState { get; set; } = LifecycleState.Draft;

    public ICollection<StudentAnswer> StudentAnswers { get; set; } = new List<StudentAnswer>();
}

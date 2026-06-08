using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Learning.Domain.Entities;

/// <summary>
/// A curriculum subject (Math, Science, Arabic, English) scoped to a single <see cref="Grade"/>.
/// Subject 1—* Unit and Subject 1—* Concept.
///
/// P8-02: Each subject now carries a stable <see cref="SubjectCode"/> and a <see cref="ContentLanguage"/>
/// forming the natural key <c>(GradeId, SubjectCode, Language)</c> for the parallel bilingual tree model.
/// Language is carried only here; child entities (Unit, Lesson, Concept, Skill, QuizQuestion) inherit it
/// via their parent chain — no language column on children.
///
/// P7-01: Added <see cref="SequenceOrder"/> (ordering within its language tree) and
/// <see cref="IsActive"/> (admin-controlled active/inactive toggle; inactive = hidden from students).
/// Soft-delete (<c>IsDeleted</c> + <c>DeletedAt</c>) is inherited from <see cref="AggregateRoot"/>
/// via <c>FullAuditedEntity</c> — no new columns required for soft-delete.
/// </summary>
public class Subject : AggregateRoot
{
    public string Name { get; set; } = null!;
    public string? Country { get; set; }

    /// <summary>Stable curriculum identifier (MATH/SCIENCE/ARABIC/ENGLISH). Stored as int.</summary>
    public SubjectCode SubjectCode { get; set; }

    /// <summary>Language of this subject tree (Ar/En). Stored as int. Language only on Subject.</summary>
    public ContentLanguage Language { get; set; }

    public int GradeId { get; set; }
    public Grade Grade { get; set; } = null!;

    /// <summary>
    /// P7-01: Display order within the <c>(GradeId, Language)</c> tree.
    /// Scoped to the language tree — Ar and En subjects have independent orderings.
    /// Default 0 (backfill-safe for existing rows).
    /// </summary>
    public int SequenceOrder { get; set; }

    /// <summary>
    /// P7-01: Admin-controlled active/inactive toggle. Inactive subjects are hidden from student-facing
    /// reads but are NOT deleted (use <c>IsDeleted</c> for soft-delete). Default <c>true</c>.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public List<Unit> Units { get; set; } = null!;
    public List<Concept> Concepts { get; set; } = null!;
}

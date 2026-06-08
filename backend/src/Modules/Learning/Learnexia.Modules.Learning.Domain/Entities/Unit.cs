using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Learning.Domain.Entities;

/// <summary>
/// An ordered teaching unit within a <see cref="Subject"/>. Unit 1—* Lesson.
///
/// P7-01: Added <see cref="IsActive"/> (admin-controlled active/inactive toggle; inactive = hidden
/// from students). <see cref="SequenceOrder"/> was already present (scoped to SubjectId).
/// Soft-delete (<c>IsDeleted</c> + <c>DeletedAt</c>) is inherited from <see cref="AggregateRoot"/>
/// via <c>FullAuditedEntity</c> — no new columns required for soft-delete.
/// </summary>
public class Unit : AggregateRoot
{
    public string Name { get; set; } = null!;
    public int SequenceOrder { get; set; }

    /// <summary>
    /// P7-01: Admin-controlled active/inactive toggle. Inactive units are hidden from student-facing
    /// reads but are NOT deleted (use <c>IsDeleted</c> for soft-delete). Default <c>true</c>.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    public List<Lesson> Lessons { get; set; } = null!;
}

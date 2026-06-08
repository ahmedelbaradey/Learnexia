using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Learning.Domain.Entities;

/// <summary>
/// A learnable skill within a <see cref="Concept"/>. Carries the mastery threshold (% required to be
/// considered mastered, cf. FR-AD-3) and an estimated time to learn. A skill may be taught by many lessons.
///
/// P7-03: Added <see cref="IsActive"/> (admin-controlled active/inactive toggle; inactive = hidden from
/// student-facing reads but NOT deleted). Mirrors the Subject/Unit/Lesson IsActive pattern from P7-01.
/// </summary>
public class Skill : AggregateRoot
{
    public string Name { get; set; } = null!;
    public int MasteryThreshold { get; set; }
    public int EstimatedTimeMinutes { get; set; }

    /// <summary>
    /// P7-03: Admin-controlled active/inactive toggle. Default <c>true</c>.
    /// Inactive skills are hidden from student-facing reads but are NOT deleted
    /// (use <c>IsDeleted</c> for soft-delete). Do NOT apply a global EF query filter on this
    /// column — admins must see inactive skills to reactivate them.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public int ConceptId { get; set; }
    public Concept Concept { get; set; } = null!;

    public List<Lesson> Lessons { get; set; } = null!;
}

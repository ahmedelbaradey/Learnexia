namespace Learnexia.Modules.Identity.Domain.Entities;

/// <summary>
/// Represents the many-to-many linkage between a parent <see cref="User"/> and a student <see cref="User"/>.
/// Composite PK (ParentId, StudentId) enforces AC-4 uniqueness.
/// Unlink / delete is out of scope for P1-04 (tracked as a follow-up story).
/// </summary>
public class ParentStudent
{
    /// <summary>FK to <see cref="User.Id"/> of the parent-role user.</summary>
    public int ParentId { get; set; }

    /// <summary>FK to <see cref="User.Id"/> of the student-role user.</summary>
    public int StudentId { get; set; }

    /// <summary>UTC timestamp when the link was created. Defaults to database now().</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Id of the user who created the link (plain int — no cross-module FK).
    /// Nullable because the link may be auto-created during onboarding before the parent user id
    /// is available in the call chain.
    /// </summary>
    public int? CreatedBy { get; set; }

    // Navigation properties — optional; kept for EF config and LINQ projections in handlers.
    public User Parent { get; set; } = null!;
    public User Student { get; set; } = null!;
}

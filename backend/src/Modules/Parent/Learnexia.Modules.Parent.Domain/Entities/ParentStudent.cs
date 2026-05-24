namespace Learnexia.Modules.Parent.Domain.Entities;

/// <summary>
/// The parent↔student linkage, owned by the Parent module (schema "parent"). Composite PK
/// (ParentId, StudentId) enforces link uniqueness. Both ids are PLAIN ints — they reference Identity's
/// User.Id by value, with NO cross-module FK or navigation property (module isolation, rule 1).
/// The Parent module never touches the Identity User table; child account reads/writes go through the
/// <c>IChildAccountService</c> Shared.Contracts seam.
/// </summary>
public class ParentStudent
{
    /// <summary>Id of the parent-role user (plain int — no cross-module FK).</summary>
    public int ParentId { get; set; }

    /// <summary>Id of the student-role user (plain int — no cross-module FK).</summary>
    public int StudentId { get; set; }

    /// <summary>UTC timestamp when the link was created. Defaults to database now().</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Id of the user who created the link (plain int — no FK constraint).</summary>
    public int? CreatedBy { get; set; }
}

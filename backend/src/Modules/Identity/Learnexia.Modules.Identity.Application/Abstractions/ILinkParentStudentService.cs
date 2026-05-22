using Learnexia.Modules.Identity.Domain.Entities;

namespace Learnexia.Modules.Identity.Application.Abstractions;

/// <summary>
/// Creates and reads the parent↔student linkage (the <c>ParentStudent</c> join row).
/// Lives in Application Abstractions so downstream consumers (P1-03's add-child handler)
/// can call <see cref="LinkAsync"/> as the auto-link hook without referencing Infrastructure.
/// </summary>
/// <remarks>
/// UoW sequencing (ADR 0001 / plan §"Critical dependency note"): the implementation commits the
/// <c>ParentStudent</c> row immediately via the module DbContext's <c>SaveChangesAsync(userId)</c>
/// — it does NOT defer to <c>UnitOfWorkBehavior</c>. P1-03 must therefore call <see cref="LinkAsync"/>
/// only AFTER ASP.NET Identity's <c>UserManager.CreateAsync</c> for the child has completed (which
/// commits the child user immediately), so the FK target exists.
/// </remarks>
public interface ILinkParentStudentService
{
    /// <summary>
    /// Idempotently links a parent to a student. Creates a <c>ParentStudent(parentId, studentId)</c>
    /// row if one does not already exist; a no-op if the pair is already linked (AC-6).
    /// Commits immediately.
    /// </summary>
    /// <returns><c>true</c> if a new link row was created; <c>false</c> if the link already existed.</returns>
    Task<bool> LinkAsync(int parentId, int studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>true</c> when a <c>ParentStudent(parentId, studentId)</c> link row exists.
    /// </summary>
    Task<bool> IsLinkedAsync(int parentId, int studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>true</c> when the given student is already linked to ANY parent
    /// (used by the link-existing-child fail-closed guard, AC-7).
    /// </summary>
    Task<bool> StudentHasAnyParentAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the student <see cref="User"/>s linked to the given parent — scoped strictly to that
    /// parent (AC-3 family isolation). Empty when the parent has no linked children.
    /// </summary>
    Task<IReadOnlyList<User>> GetLinkedStudentsAsync(int parentId, CancellationToken cancellationToken = default);
}

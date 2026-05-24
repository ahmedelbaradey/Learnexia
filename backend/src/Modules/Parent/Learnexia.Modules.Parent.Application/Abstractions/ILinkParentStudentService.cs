namespace Learnexia.Modules.Parent.Application.Abstractions;

/// <summary>
/// Creates and reads the parent↔student linkage (the <c>ParentStudent</c> join row) owned by the Parent
/// module's <c>ParentDbContext</c> (schema "parent"). Relocated from Identity; it no longer touches the
/// Identity User table. <c>GetLinkedStudentsAsync</c> returns child ids only — the caller enriches them
/// into profiles via the <c>IChildAccountService</c> Shared.Contracts seam (no cross-module entity nav).
/// </summary>
/// <remarks>
/// Writes commit immediately via <c>ParentDbContext.SaveChangesAsync(userId)</c> (the original semantics),
/// not via the deferred <c>UnitOfWorkBehavior</c>: add-child links a child only AFTER the child account
/// has already been created through the seam, so the link write is its own committed unit.
/// </remarks>
public interface ILinkParentStudentService
{
    /// <summary>
    /// Idempotently links a parent to a student. Creates a <c>ParentStudent(parentId, studentId)</c>
    /// row if one does not already exist; a no-op if the pair is already linked. Commits immediately.
    /// </summary>
    /// <returns><c>true</c> if a new link row was created; <c>false</c> if the link already existed.</returns>
    Task<bool> LinkAsync(int parentId, int studentId, CancellationToken cancellationToken = default);

    /// <summary>Returns <c>true</c> when a <c>ParentStudent(parentId, studentId)</c> link row exists.</summary>
    Task<bool> IsLinkedAsync(int parentId, int studentId, CancellationToken cancellationToken = default);

    /// <summary>Returns <c>true</c> when the given student is already linked to ANY parent.</summary>
    Task<bool> StudentHasAnyParentAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>Returns <c>true</c> when the given parent has at least one linked child.</summary>
    Task<bool> ParentHasAnyChildAsync(int parentId, CancellationToken cancellationToken = default);

    /// <summary>Counts the distinct parents linked to the given student (last-parent guard input).</summary>
    Task<int> CountParentsForStudentAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the <c>ParentStudent(parentId, studentId)</c> link row. Commits immediately.
    /// </summary>
    /// <returns><c>true</c> if a row was removed; <c>false</c> if no such link existed.</returns>
    Task<bool> UnlinkAsync(int parentId, int studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically checks whether the student has more than one parent linked and, if so, removes the
    /// <c>ParentStudent(parentId, studentId)</c> link row — all within a REPEATABLE READ transaction to
    /// prevent the TOCTOU race that would otherwise allow two concurrent unlinks to both pass the
    /// last-parent guard and orphan the child. Returns <see langword="true"/> when the unlink was
    /// performed, <see langword="false"/> when blocked (student has only one parent remaining).
    /// </summary>
    Task<bool> UnlinkIfNotLastParentAsync(int parentId, int studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the student user ids linked to the given parent — strictly parent-scoped. Empty when the
    /// parent has no linked children. Profiles are resolved separately via <c>IChildAccountService</c>.
    /// </summary>
    Task<IReadOnlyList<int>> GetLinkedStudentIdsAsync(int parentId, CancellationToken cancellationToken = default);
}

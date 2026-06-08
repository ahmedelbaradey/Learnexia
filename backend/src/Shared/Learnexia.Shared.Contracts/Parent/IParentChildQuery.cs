namespace Learnexia.Shared.Contracts.Parent;

/// <summary>
/// Cross-module seam (mirrors <see cref="Identity.IUserLookup"/>) exposing read-only parent↔child link
/// facts owned by the Parent module. Lets Identity's self-scoped Me endpoint surface <c>HasChildren</c>
/// after the <c>ParentStudent</c> link table moved out of Identity into the Parent module — without
/// Identity referencing the Parent module's projects. Implemented in <c>Parent.Infrastructure</c> and
/// registered at the Host. Strictly parent-scoped (the caller passes its own JWT-resolved id) — no IDOR.
/// </summary>
public interface IParentChildQuery
{
    /// <summary>
    /// Returns <c>true</c> when the given parent has at least one linked child
    /// (any <c>ParentStudent</c> row with <c>ParentId == parentId</c>).
    /// </summary>
    Task<bool> ParentHasAnyChildAsync(int parentId, CancellationToken ct = default);

    /// <summary>
    /// Returns <c>true</c> if the JWT-authenticated parent is linked to the given child.
    /// Used by P4-09 prefs endpoints to gate parent writes (JWT-role-Parent must match
    /// the ChildId via this link check; child JWTs cannot write parent-controlled prefs).
    /// Anti-IDOR: callers should return a generic 403 on false regardless of whether the
    /// child exists — do not distinguish "child not found" from "not your child".
    /// </summary>
    Task<bool> IsParentOfChildAsync(int parentId, int childId, CancellationToken ct = default);

    /// <summary>
    /// Reverse lookup: given a child (student) id, return the linked parent id.
    /// Used by P4-09 cross-module integration-event consumers in Notifications to
    /// determine which parent owns the reengagement preferences for the student
    /// whose event fired. Returns <c>null</c> if the child has no linked parent (orphan
    /// account or pre-link state).
    /// </summary>
    Task<int?> FindParentForChildAsync(int childId, CancellationToken ct = default);

    /// <summary>
    /// Returns all student (child) user ids linked to the given parent.
    /// Added for P7-06 admin inspect: lets Identity's family query surface the full
    /// list of children for a parent without referencing the Parent module's projects.
    /// Returns an empty list when the parent has no linked children.
    /// </summary>
    Task<IReadOnlyList<int>> GetChildIdsForParentAsync(int parentUserId, CancellationToken ct = default);

    /// <summary>
    /// Returns all parent user ids linked to the given child (student).
    /// Added for P7-06 admin inspect: a child may have more than one parent.
    /// Returns an empty list when the child has no linked parents (orphan state).
    /// </summary>
    Task<IReadOnlyList<int>> GetParentIdsForChildAsync(int childUserId, CancellationToken ct = default);
}

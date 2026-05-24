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
}

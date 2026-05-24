namespace Learnexia.Shared.Contracts.Identity;

/// <summary>
/// Cross-module seam (mirrors <see cref="IUserLookup"/>) that lets the Parent module create, read and
/// update child <c>User</c> accounts WITHOUT referencing the Identity module's projects or entities.
/// Implemented in <c>Identity.Infrastructure</c> (<c>IdentityChildAccountService</c>) and registered at
/// the Host. All results are SHAPED — raw ASP.NET Identity error descriptions are NEVER leaked across
/// the boundary (they can carry password/identity internals). The acting parent id is always supplied
/// by the caller from the JWT, never trusted from a request body.
/// </summary>
public interface IChildAccountService
{
    /// <summary>
    /// Provisions a new Student-role child account. Encapsulates the duplicate-email check,
    /// UserManager.CreateAsync (password hashed, RegistrationIsCompleted=false, CreatedBy=ActingParentId),
    /// AddToRoleAsync(Student) with a compensating DeleteAsync on role-assign failure, and a best-effort
    /// UserRegisteredIntegrationEvent publish. Returns a shaped <see cref="ChildAccountResult"/>.
    /// </summary>
    Task<ChildAccountResult> CreateChildAsync(CreateChildRequest req, CancellationToken ct = default);

    /// <summary>
    /// Resolves the child profiles for the supplied user ids (e.g. the link rows the Parent module owns).
    /// Order is not guaranteed; ids with no matching user are omitted. Read-only.
    /// </summary>
    Task<IReadOnlyList<ChildProfile>> GetChildrenAsync(IReadOnlyList<int> childIds, CancellationToken ct = default);

    /// <summary>
    /// Resolves the facts the Parent module's Link-Child guard needs for a candidate child identified by
    /// email, WITHOUT leaking Identity internals: whether a user exists, whether it holds the Student role,
    /// the user id, and which parent (if any) created it. Returns <c>null</c> when no user matches the email.
    /// The Parent handler maps every rejection to a single generic message (anti-enumeration).
    /// </summary>
    Task<LinkableChild?> FindLinkableChildByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Updates the editable profile fields (FullName/Grade/Language/Country) of an existing child user.
    /// Email/login/role are intentionally NOT mutable here (no mass-assignment). Returns a shaped result.
    /// </summary>
    Task<ChildAccountResult> UpdateChildAsync(UpdateChildRequest req, CancellationToken ct = default);
}

/// <summary>Request to provision a new child account. <c>ActingParentId</c> is the JWT-resolved parent.</summary>
public sealed record CreateChildRequest(
    string Email,
    string Password,
    string FullName,
    string Language,
    string Country,
    int Grade,
    int ActingParentId);

/// <summary>Request to update an existing child's editable profile fields.</summary>
public sealed record UpdateChildRequest(
    int ChildUserId,
    string FullName,
    int Grade,
    string Language,
    string Country);

/// <summary>
/// Minimal facts about a candidate child for the Link-Child guard. <c>CreatedByParentId</c> is the
/// id of the parent that originally created the account (null if not parent-created).
/// </summary>
public sealed record LinkableChild(
    int Id,
    bool IsStudent,
    int? CreatedByParentId);

/// <summary>A child user profile projection. <c>Language</c> echoes the stored culture code; <c>Country</c> echoes Nationality.</summary>
public sealed record ChildProfile(
    int Id,
    string FullName,
    string Email,
    int? Grade,
    string Language,
    string Country);

/// <summary>
/// Shaped outcome of a create/update operation. On failure, <see cref="ErrorCode"/> is a stable,
/// non-sensitive discriminator the Parent handler maps to a localized message — raw Identity error
/// descriptions are never surfaced.
/// </summary>
public sealed record ChildAccountResult(
    bool Succeeded,
    int ChildUserId,
    ChildAccountError ErrorCode = ChildAccountError.None,
    ChildProfile? Profile = null);

/// <summary>Stable, non-sensitive failure discriminators for child account operations.</summary>
public enum ChildAccountError
{
    None = 0,
    DuplicateEmail = 1,
    CreateFailed = 2,
    RoleAssignFailed = 3,
    NotFound = 4,
    UpdateFailed = 5,
}

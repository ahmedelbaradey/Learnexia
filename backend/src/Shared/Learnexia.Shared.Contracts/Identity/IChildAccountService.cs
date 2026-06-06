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

    /// <summary>
    /// Changes the medium-of-instruction language (<c>User.LearningLanguage</c>) for an existing child.
    ///
    /// Contract:
    ///   1. Resolves the child by <paramref name="childUserId"/>; missing → <c>NotFound</c>.
    ///   2. Captures old language; if <c>newLearningLanguage</c> already equals the current value,
    ///      returns a success no-op result (OldLanguage == NewLanguage) — no publish, no reset.
    ///   3. Sets <c>User.LearningLanguage = newLearningLanguage</c> + <c>UpdatedAt = UtcNow</c>
    ///      and calls <c>UpdateAsync</c> (commits immediately — Identity has no UoW).
    ///   4. After the commit, best-effort-publishes a <see cref="LearningLanguageChangedIntegrationEvent"/>
    ///      (mirrors <c>PublishUserRegisteredEventAsync</c>: try/catch/log; publish failure never
    ///      rolls back the committed change).
    ///   5. Returns <see cref="ChildLanguageChangeResult"/> with old and new language codes.
    ///
    /// The confirm-gate (<c>ConfirmFreshStart</c>) is enforced in the handler BEFORE this method
    /// is called — the seam itself is safe to call even without an explicit gate.
    /// </summary>
    Task<ChildLanguageChangeResult> ChangeLearningLanguageAsync(
        int childUserId,
        string newLearningLanguage,
        CancellationToken ct = default);
}

/// <summary>Request to provision a new child account. <c>ActingParentId</c> is the JWT-resolved parent.</summary>
/// <param name="LearningLanguage">
/// Medium-of-instruction language for curriculum delivery ("ar" or "en").
/// Distinct from <paramref name="Language"/> (the UI/UX language preference).
/// Required; set by the parent at onboarding; immutable by the student (parent-only change is P8-04).
/// </param>
public sealed record CreateChildRequest(
    string Email,
    string Password,
    string FullName,
    string Language,
    string Country,
    int Grade,
    int ActingParentId,
    string LearningLanguage);

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

/// <summary>
/// A child user profile projection. <c>Language</c> echoes the stored UI culture code; <c>Country</c>
/// echoes Nationality. <c>LearningLanguage</c> is the medium-of-instruction code ("ar"/"en", axis B) —
/// the Parent module surfaces it so the parent can see/change it (P8-04); distinct from <c>Language</c>.
/// </summary>
public sealed record ChildProfile(
    int Id,
    string FullName,
    string Email,
    int? Grade,
    string Language,
    string Country,
    string LearningLanguage);

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
    LanguageUpdateFailed = 6,
}

/// <summary>
/// Shaped outcome of <see cref="IChildAccountService.ChangeLearningLanguageAsync"/>.
/// On success, <see cref="OldLanguage"/> and <see cref="NewLanguage"/> carry the before/after codes.
/// For same-language no-ops, <see cref="IsNoOp"/> is <c>true</c> and both values are equal.
/// </summary>
public sealed record ChildLanguageChangeResult(
    bool Succeeded,
    int ChildUserId,
    string OldLanguage,
    string NewLanguage,
    bool IsNoOp = false,
    ChildAccountError ErrorCode = ChildAccountError.None);

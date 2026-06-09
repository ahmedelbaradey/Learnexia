using Learnexia.Modules.Identity.Domain.Enums;

namespace Learnexia.Modules.Identity.Application.Features.Users.Dtos.Admin;

/// <summary>
/// Admin-specific read-only profile DTO (P7-06 BE-2).
/// Kept independent of the self-service <c>UserProfileResponseDto</c> so admin-inspect
/// fields (both language axes, status history, grade/country) are not leaked to the
/// self-service /Me endpoint projection (design decision per brief Q-A7).
///
/// Both language fields are surfaced distinctly:
///   <c>PreferredLanguage</c> — UI/UX language preference (culture code, e.g. "ar-EG").
///   <c>LearningLanguage</c>  — medium-of-instruction for the child's curriculum ("ar"/"en").
///   These are two independent axes; the FE labels them separately.
///
/// <c>LastSignInAtUtc</c> is null because the User entity has no LastSignInAt column.
/// Sign-in timestamps are labeled "not tracked" per plan decision (Q-A6 baked-in).
/// </summary>
public sealed record AdminUserProfileDto
{
    public int Id { get; init; }
    public string FullName { get; init; } = null!;
    public string Email { get; init; } = null!;

    /// <summary>Primary role name (e.g. "Parent", "Student", "Admin"). Null if no role assigned.</summary>
    public string? Role { get; init; }

    /// <summary>All roles assigned to this user.</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>Governance lifecycle status.</summary>
    public AccountStatus AccountStatus { get; init; }

    /// <summary>Whether the account can sign in (mechanical gate; kept in sync with AccountStatus).</summary>
    public bool IsActive { get; init; }

    /// <summary>Reason for the last status change (suspend/delete). Null if status is Active.</summary>
    public string? LastStatusReason { get; init; }

    /// <summary>UTC timestamp of the last status change. Null if never changed from Active.</summary>
    public DateTime? StatusChangedAtUtc { get; init; }

    /// <summary>UTC timestamp when the account was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Always null — the User entity has no LastSignInAt column.
    /// Sign-in activity is not tracked at the Identity layer in this wave (P7-06 Q-A6 baked-in decision).
    /// </summary>
    public DateTime? LastSignInAtUtc => null;

    // ── Language axes (distinct — do not merge) ──────────────────────────────

    /// <summary>UI/UX language preference. Culture code e.g. "ar-EG" or "en-US".</summary>
    public string PreferredLanguage { get; init; } = null!;

    /// <summary>
    /// Medium-of-instruction language for the child's curriculum ("ar" or "en").
    /// For parents/admins this may be the system default "ar"; the FE labels it "child only".
    /// </summary>
    public string LearningLanguage { get; init; } = null!;

    // ── Child-only fields (null for parents/admins) ───────────────────────────

    /// <summary>School grade (1–6). Populated for Student role; null for others.</summary>
    public int? Grade { get; init; }

    /// <summary>Nationality / country field. Populated for Student role.</summary>
    public string? Nationality { get; init; }

    public string? AvatarUrl { get; init; }
}

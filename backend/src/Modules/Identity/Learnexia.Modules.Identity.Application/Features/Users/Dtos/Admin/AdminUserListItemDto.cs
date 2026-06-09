using Learnexia.Modules.Identity.Domain.Enums;

namespace Learnexia.Modules.Identity.Application.Features.Users.Dtos.Admin;

/// <summary>
/// Lean list-item DTO for the admin user search endpoint (P7-06 BE-1).
/// Carries MINIMAL child PII — no grade, language, or country in the list view.
/// Those fields are only surfaced on the single-profile detail endpoint to limit
/// child data exposure in broad paginated results (security decision per brief Q-A1).
/// Status maps to IsActive until P7-07 upgrades the filter to AccountStatus.
/// </summary>
public sealed record AdminUserListItemDto
{
    public int Id { get; init; }
    public string FullName { get; init; } = null!;
    public string Email { get; init; } = null!;

    /// <summary>Primary role name (e.g. "Parent", "Student"). Null if no role assigned.</summary>
    public string? Role { get; init; }

    /// <summary>
    /// Account status based on <c>AccountStatus</c> field.
    /// Until P7-07 lands, callers may also observe IsActive for active/inactive distinctions.
    /// </summary>
    public AccountStatus AccountStatus { get; init; }

    /// <summary>UTC timestamp when the account was created.</summary>
    public DateTime CreatedAt { get; init; }
}

namespace Learnexia.Modules.Identity.Application.Features.Account.Dtos;

/// <summary>
/// Self-scoped account-profile contract for the authenticated user (P1-12 BE-1). Returned by both
/// the GET profile query and the update command so the FE can refresh from a single shape.
/// <c>Phone</c> echoes the inherited Identity <c>PhoneNumber</c>; <c>Country</c> echoes
/// <c>Nationality</c>; <c>AvatarUrl</c> is read-only here (set later by the avatar-upload endpoint,
/// BE-4) and is null until the user uploads an avatar. No id/email/role is accepted on update — the
/// user is always resolved from the JWT, never the body (no mass-assignment, no IDOR).
/// </summary>
public record AccountProfileResponse
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Country { get; set; }
    public string? AvatarUrl { get; set; }
}

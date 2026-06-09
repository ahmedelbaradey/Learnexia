namespace Learnexia.Modules.Identity.Application.Features.Users.Dtos.Admin;

/// <summary>
/// Family linkage DTO for the admin family endpoint (P7-06 BE-3).
/// For a parent: <c>Children</c> is populated; <c>Parents</c> is empty.
/// For a child:  <c>Parents</c> is populated; <c>Children</c> is empty.
/// Family data is sourced from the Parent module via <c>IParentChildQuery</c> seam —
/// no cross-module FK.
/// </summary>
public sealed record AdminFamilyDto
{
    public int UserId { get; init; }

    /// <summary>Role of the queried user ("Parent" or "Student").</summary>
    public string UserRole { get; init; } = null!;

    /// <summary>
    /// Children linked to this user (populated when <c>UserRole == "Parent"</c>).
    /// Each entry is a minimal child summary — just enough for admin triage.
    /// </summary>
    public IReadOnlyList<AdminFamilyMemberDto> Children { get; init; } = [];

    /// <summary>
    /// Parents linked to this user (populated when <c>UserRole == "Student"</c>).
    /// A child may have more than one linked parent.
    /// </summary>
    public IReadOnlyList<AdminFamilyMemberDto> Parents { get; init; } = [];
}

/// <summary>
/// Minimal summary of a family member (parent or child) for the admin inspect view.
/// Carries only the fields an admin needs for support triage — no unnecessary PII.
///
/// Finding #6 — child email omitted from the family list view.
/// <c>Email</c> is non-null only for parent members; it is <c>null</c> for children.
/// An admin needing a child's email must open the individual profile endpoint (/Admin/Users/{id}).
/// This prevents the family list from broadcasting every child's email in a single response.
/// </summary>
public sealed record AdminFamilyMemberDto(
    int Id,
    string FullName,
    string? Email,
    int? Grade);

namespace Learnexia.Modules.Identity.Application.Features.Users.Dtos.Admin;

/// <summary>
/// Response returned by <c>OverrideChildGradeCommand</c> on success.
/// Contains only opaque ids and grade codes — no PII (P7-08 security rule).
/// </summary>
public sealed record ChildGradeOverrideResponseDto(
    int ChildUserId,
    int OldGrade,
    int NewGrade);

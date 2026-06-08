using Learnexia.Modules.Identity.Application.Features.Users.Dtos.Admin;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.OverrideChildGrade;

/// <summary>
/// Admin command to override a child's grade (P7-08 BE-3).
///
/// This is a NON-DESTRUCTIVE operation:
///   - Updates <c>User.Grade</c> to the new value and commits.
///   - Publishes <c>ChildGradeChangedIntegrationEvent</c> post-commit (best-effort).
///   - Does NOT delete XP, badges, streaks, mastery, or any learning history.
///   - Learning re-scoping (new grade's curriculum) takes effect at the next read
///     (the student passes the grade to subject queries — no persisted grade-scoped
///     materialization needs refreshing — see Q-C2 finding in the brief).
///
/// Confirm is a soft UX guard (not a 424 gate):
///   - <c>Confirm = false</c> → HTTP 400 BadRequest (no mutation, no event).
///   - <c>Confirm = true</c>  → mutation proceeds.
///   This is distinct from the 424 confirm-gate on the destructive learning-language change.
///
/// Security:
///   - Authorized via class-level <c>AdminOnly</c> on <c>AdminUsersController</c>.
///   - No parent-link IDOR — admin acts on any child.
///   - Validates the target user holds the Student role.
///   - No-op rejected: same grade as current → BadRequest (prevents phantom events).
/// </summary>
public record OverrideChildGradeCommand : ICommand<BaseResponse<ChildGradeOverrideResponseDto>>
{
    public int ChildId { get; init; }

    /// <summary>Target grade (1–6 inclusive per product rules).</summary>
    public int Grade { get; init; }

    /// <summary>
    /// Admin-supplied reason for the override. Stored in the audit event.
    /// Optional (null allowed); max 500 chars.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Soft UX confirmation flag. Must be <c>true</c> for the override to proceed.
    /// <c>false</c> returns HTTP 400 with <c>ChildGradeOverrideConfirmRequired</c> — no mutation.
    /// This is NOT a 424 gate (grade override is non-destructive).
    /// </summary>
    public bool Confirm { get; init; }
}

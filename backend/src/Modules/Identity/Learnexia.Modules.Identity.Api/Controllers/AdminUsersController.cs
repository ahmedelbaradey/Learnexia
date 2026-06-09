using Learnexia.Modules.Identity.Api.Bases;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.AdminChangeLearningLanguage;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.DeleteAccount;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.OverrideChildGrade;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.ReactivateAccount;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.SuspendAccount;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.UpdateChildProfile;
using Learnexia.Modules.Identity.Application.Features.Users.Dtos.Admin;
using Learnexia.Modules.Identity.Application.Features.Users.Queries.AdminGetUserActivity;
using Learnexia.Modules.Identity.Application.Features.Users.Queries.AdminGetUserFamily;
using Learnexia.Modules.Identity.Application.Features.Users.Queries.AdminGetUserProfile;
using Learnexia.Modules.Identity.Application.Features.Users.Queries.AdminSearchUsers;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Identity.Api.Controllers;

/// <summary>
/// Admin REST surface for user search, inspect (P7-06), account lifecycle (P7-07),
/// and child profile/grade/language management (P7-08).
/// Route: <c>api/Admin/Users</c> — agreed FE contract for the user/account wave.
/// All endpoints are class-level <c>AdminOnly</c> gated; anonymous → 401; non-admin → 403.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/Admin/Users")]
[ApiController]
public class AdminUsersController : AppControllerBase
{
    // ── P7-06 Read endpoints ─────────────────────────────────────────────────

    /// <summary>
    /// GET api/Admin/Users
    /// Paginated, filterable search over all non-Deleted users.
    /// Filters: role, status (AccountStatus int), q (free-text over name + email).
    /// Returns <c>PaginatedResult&lt;AdminUserListItemDto&gt;</c> with MINIMAL child PII.
    /// Page size capped at 100 in the handler.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<AdminUserListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchUsers([FromQuery] SearchUsersQuery query)
        => NewResult(await Mediator.Send(query));

    /// <summary>
    /// GET api/Admin/Users/{id}
    /// Read-only admin-inspect profile for a single user.
    /// Includes both <c>preferredLanguage</c> and <c>learningLanguage</c> shown distinctly.
    /// Emits <c>AdminActionPerformedEvent(UserViewed)</c> post-read (best-effort).
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BaseResponse<AdminUserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserProfile(int id)
        => NewResult(await Mediator.Send(new GetAdminUserProfileQuery { UserId = id }));

    /// <summary>
    /// GET api/Admin/Users/{id}/family
    /// Family linkage for a single user.
    /// Parent → children[]; Child → parents[].
    /// Cross-module data sourced from Parent module via <c>IParentChildQuery</c> seam.
    /// </summary>
    [HttpGet("{id:int}/family")]
    [ProducesResponseType(typeof(BaseResponse<AdminFamilyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserFamily(int id)
        => NewResult(await Mediator.Send(new GetUserFamilyQuery { UserId = id }));

    /// <summary>
    /// GET api/Admin/Users/{id}/activity
    /// Activity summary for a single user.
    /// Composed from Gamification seams; degrades gracefully (missing seam → null field, never 500).
    /// <c>lastSignInAtUtc</c> is always null (not tracked in this wave — P7-06 Q-A6).
    /// </summary>
    [HttpGet("{id:int}/activity")]
    [ProducesResponseType(typeof(BaseResponse<AdminActivitySummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserActivity(int id)
        => NewResult(await Mediator.Send(new GetUserActivitySummaryQuery { UserId = id }));

    // ── P7-07 Account lifecycle write endpoints ─────────────────────────────

    /// <summary>
    /// POST api/Admin/Users/{id}/suspend
    /// Suspend an account: AccountStatus → Suspended, IsActive = false.
    /// Revokes the Redis refresh token and terminates all tracked sessions.
    /// Requires a non-empty reason (max 500 chars).
    /// Rejected if already Deleted (terminal) or already Suspended.
    /// An admin cannot suspend their own account; SuperAdmin is protected.
    /// Emits <c>AccountSuspendedIntegrationEvent</c> + <c>AdminActionPerformedEvent</c>.
    /// </summary>
    [HttpPost("{id:int}/suspend")]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SuspendAccount(int id, [FromBody] SuspendAccountRequest body)
        => NewResult(await Mediator.Send(new SuspendAccountCommand
        {
            UserId = id,
            Reason = body.Reason,
        }));

    /// <summary>
    /// POST api/Admin/Users/{id}/reactivate
    /// Reactivate a suspended account: AccountStatus → Active, IsActive = true.
    /// Rejected if the account is Deleted (terminal state).
    /// Emits <c>AccountReactivatedIntegrationEvent</c> + <c>AdminActionPerformedEvent</c>.
    /// </summary>
    [HttpPost("{id:int}/reactivate")]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReactivateAccount(int id, [FromBody] ReactivateAccountRequest body)
        => NewResult(await Mediator.Send(new ReactivateAccountCommand
        {
            UserId = id,
            Reason = body.Reason,
        }));

    /// <summary>
    /// DELETE api/Admin/Users/{id}
    /// Soft-delete an account: AccountStatus → Deleted, IsActive = false.
    /// Two-step confirm gate: Confirm = false → HTTP 424 (no mutation).
    /// For Parent accounts with CascadeChildren = true, all linked children are also soft-deleted
    /// in the same transaction.
    /// Soft-delete only — no physical row removal, learning history is preserved.
    /// An admin cannot delete their own account; SuperAdmin is protected.
    /// Emits <c>AccountDeletedIntegrationEvent</c> + <c>AdminActionPerformedEvent</c>.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status424FailedDependency)]
    public async Task<IActionResult> DeleteAccount(int id, [FromBody] DeleteAccountRequest body)
        => NewResult(await Mediator.Send(new DeleteAccountCommand
        {
            UserId = id,
            Reason = body.Reason,
            Confirm = body.Confirm,
            CascadeChildren = body.CascadeChildren,
        }));
    // ── P7-08 Child profile / grade / learning-language write endpoints ─────────

    /// <summary>
    /// PATCH api/Admin/Users/{childId}/profile
    /// Update the harmless profile fields of a child account:
    ///   - <c>preferredLanguage</c> (UI/UX language — NOT the curriculum language)
    ///   - <c>country</c> / nationality
    /// No learning progress is affected. No event emitted.
    /// Requires the target user to hold the Student role.
    /// Emits <c>AdminActionPerformedEvent(ChildProfileUpdated)</c>.
    /// </summary>
    [HttpPatch("{childId:int}/profile")]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateChildProfile(int childId, [FromBody] UpdateChildProfileRequest body)
        => NewResult(await Mediator.Send(new UpdateChildProfileCommand
        {
            ChildId = childId,
            PreferredLanguage = body.PreferredLanguage,
            Country = body.Country,
        }));

    /// <summary>
    /// POST api/Admin/Users/{childId}/grade
    /// Override a child's grade (NON-DESTRUCTIVE).
    /// Learning history (XP, badges, streaks, mastery, attempts) is preserved.
    /// <c>confirm = false</c> → HTTP 400 (soft UX guard — NOT a 424 gate).
    /// <c>confirm = true</c>  → grade updated; <c>ChildGradeChangedIntegrationEvent</c> emitted.
    /// Requires the target user to hold the Student role.
    /// Invalid grade (outside 1–6) → HTTP 422.
    /// Same grade as current → HTTP 400 (no phantom event).
    /// Emits <c>AdminActionPerformedEvent(ChildGradeOverridden)</c>.
    /// </summary>
    [HttpPost("{childId:int}/grade")]
    [ProducesResponseType(typeof(BaseResponse<ChildGradeOverrideResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> OverrideChildGrade(int childId, [FromBody] OverrideChildGradeRequest body)
        => NewResult(await Mediator.Send(new OverrideChildGradeCommand
        {
            ChildId = childId,
            Grade = body.Grade,
            Reason = body.Reason,
            Confirm = body.Confirm,
        }));

    /// <summary>
    /// POST api/Admin/Users/{childId}/learning-language
    /// Change a child's <c>LearningLanguage</c> (medium of instruction — DESTRUCTIVE).
    /// Mirrors the parent P8-04 path: confirm-gate fires FIRST (before any mutation).
    /// <c>confirmFreshStart = false</c> → HTTP 424 (no mutation, no event, no reset).
    /// <c>confirmFreshStart = true</c>  → commits the language change via the Identity seam;
    ///   Learning consumer hard-deletes Math/Science attempts; Arabic/English + gamification retained.
    /// Same language → no-op success (no event, no reset).
    /// Unsupported language value → HTTP 422.
    /// Requires the target user to hold the Student role.
    /// Emits <c>LearningLanguageChangedIntegrationEvent</c> + <c>AdminActionPerformedEvent(ChildLearningLanguageChanged)</c>.
    ///
    /// Learning-language claim staleness (Q-C3): the new value lands on the child's next
    /// token (refresh/sign-in); existing access tokens retain the old value until expiry
    /// (stateless JWT — same bounded-staleness as parent P8-04).
    /// </summary>
    [HttpPost("{childId:int}/learning-language")]
    [ProducesResponseType(typeof(BaseResponse<AdminChangedLearningLanguageResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status424FailedDependency)]
    public async Task<IActionResult> ChangeChildLearningLanguage(
        int childId,
        [FromBody] AdminChangeLearningLanguageRequest body)
        => NewResult(await Mediator.Send(new AdminChangeLearningLanguageCommand
        {
            ChildId = childId,
            LearningLanguage = body.LearningLanguage,
            ConfirmFreshStart = body.ConfirmFreshStart,
        }));
}

// ── Request body DTOs (inline — thin, admin-only) ────────────────────────────

/// <summary>Request body for POST …/{id}/suspend.</summary>
public record SuspendAccountRequest
{
    public string Reason { get; init; } = null!;
}

/// <summary>Request body for POST …/{id}/reactivate.</summary>
public record ReactivateAccountRequest
{
    public string? Reason { get; init; }
}

/// <summary>Request body for DELETE …/{id}.</summary>
public record DeleteAccountRequest
{
    public string Reason { get; init; } = null!;
    public bool Confirm { get; init; }
    public bool CascadeChildren { get; init; }
}

/// <summary>Request body for PATCH …/{childId}/profile (P7-08).</summary>
public record UpdateChildProfileRequest
{
    /// <summary>UI/UX preferred language. Accepted: "ar" | "en" | "ar-EG" | "en-US". Null = no change.</summary>
    public string? PreferredLanguage { get; init; }

    /// <summary>Country / nationality. Null = no change.</summary>
    public string? Country { get; init; }
}

/// <summary>Request body for POST …/{childId}/grade (P7-08).</summary>
public record OverrideChildGradeRequest
{
    /// <summary>Target grade (1–6). Required.</summary>
    public int Grade { get; init; }

    /// <summary>Admin-supplied reason for the override. Optional; max 500 chars.</summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Soft UX confirm flag. Must be <c>true</c> for the override to proceed.
    /// <c>false</c> → HTTP 400 (no mutation). Not a 424 — grade override is non-destructive.
    /// </summary>
    public bool Confirm { get; init; }
}

/// <summary>Request body for POST …/{childId}/learning-language (P7-08).</summary>
public record AdminChangeLearningLanguageRequest
{
    /// <summary>Target learning language. Accepted: "ar" | "en".</summary>
    public string LearningLanguage { get; init; } = null!;

    /// <summary>
    /// Destructive-operation confirm gate. Must be <c>true</c> for the change to proceed.
    /// <c>false</c> → HTTP 424 FailedDependency (no mutation, no Math/Science reset).
    /// </summary>
    public bool ConfirmFreshStart { get; init; }
}

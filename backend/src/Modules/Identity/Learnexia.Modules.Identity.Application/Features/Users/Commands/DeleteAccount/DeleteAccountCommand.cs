using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.DeleteAccount;

/// <summary>
/// Admin command to soft-delete a user account (P7-07 BE-4).
///
/// Two-step confirm gate: <c>Confirm = false</c> or absent → HTTP 424 (no mutation occurs).
/// On confirmation: AccountStatus = Deleted, IsActive = false, stamps reason/actor/time,
/// revokes refresh token + sessions.
///
/// If the target is a PARENT and <c>CascadeChildren = true</c>: applies the same soft-delete
/// to all linked children via <c>IParentChildQuery.GetChildIdsForParentAsync</c>. All writes
/// are performed within the UnitOfWorkBehavior's transaction — atomicity is guaranteed.
///
/// Soft-delete only — no physical row removal, no learning-history purge (product rule:
/// preserve history; GDPR/COPPA purge is deferred to a dedicated wave).
///
/// State-machine: Active → Deleted, Suspended → Deleted. Rejected if already Deleted.
/// An admin cannot delete their own account. SuperAdmin is protected.
/// </summary>
public record DeleteAccountCommand : ICommand<BaseResponse<string>>
{
    public int UserId { get; init; }
    public string Reason { get; init; } = null!;

    /// <summary>
    /// Must be <c>true</c> for the deletion to proceed.
    /// A false (or absent) value returns HTTP 424 with a confirmation prompt.
    /// </summary>
    public bool Confirm { get; init; }

    /// <summary>
    /// When <c>true</c> and the target is a Parent account, all linked child accounts are
    /// also soft-deleted in the same transaction. Defaults to false (no cascade).
    /// </summary>
    public bool CascadeChildren { get; init; }
}

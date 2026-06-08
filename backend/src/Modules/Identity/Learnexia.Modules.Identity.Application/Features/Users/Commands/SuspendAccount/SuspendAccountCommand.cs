using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.SuspendAccount;

/// <summary>
/// Admin command to suspend a user account (P7-07 BE-2).
/// Sets AccountStatus = Suspended, IsActive = false, stamps reason/actor/time,
/// revokes the Redis refresh token and terminates all tracked sessions.
///
/// State-machine: Active → Suspended only.
/// Rejected if the target is already Deleted (terminal) or already Suspended.
/// An admin cannot suspend their own account.
/// </summary>
public record SuspendAccountCommand : ICommand<BaseResponse<string>>
{
    public int UserId { get; init; }
    public string Reason { get; init; } = null!;
}

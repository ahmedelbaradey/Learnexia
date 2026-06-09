using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.ReactivateAccount;

/// <summary>
/// Admin command to reactivate a previously suspended user account (P7-07 BE-3).
/// Sets AccountStatus = Active, IsActive = true, stamps actor/time.
///
/// State-machine: Suspended → Active only.
/// Rejected if the target is already Deleted (terminal state — deletion is irreversible).
/// </summary>
public record ReactivateAccountCommand : ICommand<BaseResponse<string>>
{
    public int UserId { get; init; }

    /// <summary>Optional reason for the reactivation (for the audit trail).</summary>
    public string? Reason { get; init; }
}

using Learnexia.Modules.Billing.Application.Features.Seats.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Seats.Commands.ReactivateChildSeat;

/// <summary>
/// Command: parent initiates a prorated reactivation checkout to restore a locked child's seat (P10-15-BE-6).
///
/// <para><strong>Security / IDOR:</strong> <c>ParentUserId</c> is JWT-resolved in the handler
/// via <c>ICurrentUserService.UserId</c> — never supplied by the client. <c>ChildId</c> must
/// belong to the authenticated parent's family (validated in the service).</para>
///
/// <para><strong>Effect:</strong> creates a <c>Payment{Kind=SeatReactivation}</c> row and returns
/// a redirect URL. On webhook confirmation, the seat transitions from <c>NoSeatLocked</c> back to
/// <c>Active</c>. ZERO energy is minted (money-only reactivation).</para>
/// </summary>
public sealed record ReactivateChildSeatCommand(
    int    ChildId,
    string IdempotencyKey) : ICommand<BaseResponse<SeatCheckoutResultDto>>;

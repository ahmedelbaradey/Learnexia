using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Seats.Commands.CancelExtraSeat;

/// <summary>
/// Command: cancel one or more purchased extra seat add-ons (P10-14-BE-8).
///
/// <para><strong>Security:</strong> <c>ParentUserId</c> is JWT-resolved in the handler
/// via <c>ICurrentUserService.UserId</c> — never supplied by the client.</para>
///
/// <para><strong>Effect (cycle-end cancel):</strong> schedules <c>SeatQuantity</c> extra seats for
/// removal at the next renewal boundary. It does NOT start a grace period (grace = payment-failure
/// only), does NOT reduce the active-seat count mid-cycle, and does NOT reclaim energy or delete a
/// child. The seats stay Active for the rest of the current cycle; P10-15 applies the removal at renewal.</para>
/// </summary>
public sealed record CancelExtraSeatCommand(int SeatQuantity)
    : ICommand<BaseResponse<bool>>;

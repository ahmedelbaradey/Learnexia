using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Seats.Commands.ChooseActiveChildren;

/// <summary>
/// Command: parent explicitly selects which children hold active seats (P10-15-BE-6).
///
/// <para><strong>Security / IDOR:</strong> <c>ParentUserId</c> is JWT-resolved in the handler
/// via <c>ICurrentUserService.UserId</c> — never supplied by the client. All <c>ActiveChildIds</c>
/// must belong to the authenticated parent's family (validated in the service).</para>
///
/// <para><strong>Guard:</strong> <c>ActiveChildIds.Count</c> must not exceed <c>paidActiveSeats</c>.
/// <c>NoSeatLocked</c> children cannot be selected — they require a paid reactivation checkout first.</para>
/// </summary>
public sealed record ChooseActiveChildrenCommand(
    IReadOnlyList<int> ActiveChildIds) : ICommand<BaseResponse<bool>>;

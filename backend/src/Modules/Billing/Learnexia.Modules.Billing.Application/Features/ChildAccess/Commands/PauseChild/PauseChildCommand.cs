using Learnexia.Modules.Billing.Application.Features.ChildAccess.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.ChildAccess.Commands.PauseChild;

/// <summary>
/// Command: parent pauses a child's AI access immediately (P10-18-BE-6).
///
/// <para><strong>Security / IDOR:</strong> <c>ParentUserId</c> is JWT-resolved in the handler
/// via <c>ICurrentUserService.UserId</c> — never supplied by the client. <c>ChildId</c> must
/// belong to the authenticated parent's family (validated in the service).</para>
///
/// <para><strong>Effect:</strong> sets <c>SeatReservation.ParentPauseState = Paused</c> and
/// appends a <c>SeatLedgerEntry{ParentPause}</c>. Zero energy or seat writes.</para>
/// </summary>
public sealed record PauseChildCommand(int ChildId) : ICommand<BaseResponse<PauseResultDto>>;

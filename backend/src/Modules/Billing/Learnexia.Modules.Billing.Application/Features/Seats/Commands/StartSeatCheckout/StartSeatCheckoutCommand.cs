using Learnexia.Modules.Billing.Application.Features.Seats.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Seats.Commands.StartSeatCheckout;

/// <summary>
/// Command: initiate an extra-seat monthly add-on checkout session (P10-14-BE-6).
///
/// <para><strong>Security:</strong> <c>ParentUserId</c> is JWT-resolved in the handler
/// via <c>ICurrentUserService.UserId</c> — never supplied by the client. The extra-seat
/// price is resolved SERVER-SIDE from <c>IGlobalSettingsProvider</c> (key
/// <c>seats.extra_price_egp</c> = 169 EGP/seat). The client never supplies the price.</para>
///
/// <para><strong>Platform:</strong> web hosted checkout only — no native IAP (OQ-D/OQ-K).</para>
///
/// <para><strong>Tier gate:</strong> Free-tier parents cannot buy extra seats (OQ-C).</para>
///
/// <para><strong>Max-seats guard:</strong> <c>IncludedSeats + PurchasedExtraSeats + SeatQuantity</c>
/// must not exceed <c>seats.max=5</c> from <c>IGlobalSettingsProvider</c>.</para>
/// </summary>
public sealed record StartSeatCheckoutCommand(int SeatQuantity)
    : ICommand<BaseResponse<SeatCheckoutResultDto>>;

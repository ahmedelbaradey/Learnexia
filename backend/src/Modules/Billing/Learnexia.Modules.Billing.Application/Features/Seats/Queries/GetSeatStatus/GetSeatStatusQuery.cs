using Learnexia.Modules.Billing.Application.Features.Seats.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Seats.Queries.GetSeatStatus;

/// <summary>
/// Query: retrieve the seat-status snapshot for the authenticated parent (P10-14-BE-9).
///
/// <para><strong>Note:</strong> queries are NOT auto-validated by <c>ValidationBehavior</c>
/// (which only covers <c>ICommand&lt;&gt;</c>).</para>
/// </summary>
public sealed record GetSeatStatusQuery : IQuery<BaseResponse<SeatStatusDto>>;

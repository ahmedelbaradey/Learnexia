using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Seats.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Seats.Queries.GetSeatStatus;

/// <summary>
/// Handles <see cref="GetSeatStatusQuery"/> (P10-14-BE-9).
///
/// <para><strong>Option C — handler is EF-free.</strong> All persistence and query logic live
/// in <see cref="ISeatService.GetSeatStatusAsync"/>. This handler only resolves the parent
/// identity from the JWT and maps the result.</para>
/// </summary>
public sealed class GetSeatStatusQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetSeatStatusQuery, BaseResponse<SeatStatusDto>>
{
    private readonly ISeatService _seatService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetSeatStatusQueryHandler(
        ISeatService seatService,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _seatService  = seatService;
        _currentUser  = currentUser;
        _logger       = logger;
        _localizer    = localizer;
    }

    public async Task<BaseResponse<SeatStatusDto>> Handle(
        GetSeatStatusQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var parentId = _currentUser.UserId
                ?? throw new InvalidOperationException("Parent JWT claim missing.");

            var snapshot = await _seatService.GetSeatStatusAsync(parentId, cancellationToken);

            var children = snapshot.Children
                .Select(c => new ChildSeatItemDto(c.ChildId, c.Status)
                {
                    SeatState          = c.SeatState,
                    RemovalScheduledAt = c.RemovalScheduledAt,
                })
                .ToList();

            var dto = new SeatStatusDto(
                IncludedSeats      : snapshot.IncludedSeats,
                PurchasedExtraSeats: snapshot.PurchasedExtraSeats,
                TotalSeats         : snapshot.TotalSeats,
                OccupiedSeats      : snapshot.OccupiedSeats,
                AvailableSeats     : snapshot.AvailableSeats,
                MaxSeats           : snapshot.MaxSeats,
                Children           : children)
            {
                GraceEndsAt = snapshot.GraceEndsAt,
                IsInGrace   = snapshot.IsInGrace,
            };

            _logger.LogInfo(
                $"GetSeatStatus: parentId={parentId}, total={snapshot.TotalSeats}, occupied={snapshot.OccupiedSeats}.");

            return new BaseResponse<SeatStatusDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.SeatStatusRetrievedSuccessfully],
                Data       = dto,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error in GetSeatStatusQuery for parentUserId={_currentUser.UserId}.");
            return ServerError<SeatStatusDto>(
                _localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}

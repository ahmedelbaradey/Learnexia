using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Gamification.Application.Features.TimedEvents.Queries.ListTimedEvents;

/// <summary>
/// Handles <see cref="ListTimedEventsQuery"/> — returns the full timed-event catalog
/// for admin read use (P4-11-B4).
///
/// READ-ONLY: no <c>UnitOfWorkBehavior</c> wrap, no <c>ValidationBehavior</c> run
/// (IQuery, not ICommand). Mirrors <c>GetMyBadgesQueryHandler</c> / <c>GetMyMissionsQueryHandler</c>
/// structural shape.
///
/// No student-ID scoping — this query is catalog-wide (admin-only read). Auth is enforced
/// by <c>[Authorize]</c> on the controller; role-based scoping can be added in P7.
/// </summary>
public sealed class ListTimedEventsQueryHandler
    : BaseResponseHandler, IQueryHandler<ListTimedEventsQuery, BaseResponse<IReadOnlyList<TimedEventListItemDto>>>
{
    private readonly IGamificationRepository _repo;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ListTimedEventsQueryHandler(
        IGamificationRepository repo,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repo      = repo;
        _logger    = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<IReadOnlyList<TimedEventListItemDto>>> Handle(
        ListTimedEventsQuery request, CancellationToken ct)
    {
        try
        {
            var rows = await _repo.GetAllTimedEventsAsync(ct);

            // Hand-project entity → DTO (no AutoMapper per CONVENTIONS).
            // Scope is rendered as the enum-member name for admin readability.
            var dtos = rows
                .Select(e => new TimedEventListItemDto(
                    Id:         e.Id,
                    Code:       e.Code,
                    NameEn:     e.NameEn,
                    NameAr:     e.NameAr,
                    Multiplier: e.Multiplier,
                    StartUtc:   e.StartUtc,
                    EndUtc:     e.EndUtc,
                    IsActive:   e.IsActive,
                    Scope:      e.Scope.ToString()))
                .ToList();

            return Success((IReadOnlyList<TimedEventListItemDto>)dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "P4-11-B4: Error in ListTimedEventsQuery.");
            return ServerError<IReadOnlyList<TimedEventListItemDto>>();
        }
    }
}

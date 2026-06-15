using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.TimedEvents.Queries.ListTimedEvents;

/// <summary>
/// Handles <see cref="ListTimedEventsQuery"/> — returns the full timed-event catalog for admin read (P4-11-B4).
/// Delegates to <see cref="IGamificationAdminService"/>. Handler is EF-free and repository-free per §7 CONVENTIONS.
/// </summary>
public sealed class ListTimedEventsQueryHandler
    : BaseResponseHandler, IQueryHandler<ListTimedEventsQuery, BaseResponse<IReadOnlyList<TimedEventListItemDto>>>
{
    private readonly IGamificationAdminService _adminService;
    private readonly ILoggerManager _logger;

    public ListTimedEventsQueryHandler(
        IGamificationAdminService adminService,
        ILoggerManager logger)
    {
        _adminService = adminService;
        _logger       = logger;
    }

    public async Task<BaseResponse<IReadOnlyList<TimedEventListItemDto>>> Handle(
        ListTimedEventsQuery request, CancellationToken ct)
    {
        return await _adminService.ListTimedEventsAsync(ct);
    }
}

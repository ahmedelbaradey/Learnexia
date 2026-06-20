using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Application.Features.Reengagement.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Queries.ListMyInbox;

/// <summary>
/// Returns the authenticated user's in-app notification inbox (P4-09 B4-4 / AC4).
/// Self-scoped: RecipientExternalUserId = currentUser — no IDOR surface.
/// Paginated, newest-first. EF/pagination lives in <see cref="INotificationInboxService"/> (Option-C rule).
///
/// P9-10 (BE-4) — Localization policy:
/// This handler returns stored Title/Body verbatim (send-time text). No locale resolution happens on the
/// read path — localization is applied at send time by each producer. Forward target: re-localize on read
/// from Code+Data, owned by / sequenced with the P9-03 FE inbox consumer. No read-contract change in v1.
/// </summary>
public sealed class ListMyInboxQueryHandler
    : BaseResponseHandler,
      IQueryHandler<ListMyInboxQuery, BaseResponse<PagedInboxResult>>
{
    private readonly INotificationInboxService _inboxService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public ListMyInboxQueryHandler(
        INotificationInboxService inboxService,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _inboxService       = inboxService;
        _currentUserService = currentUserService;
        _localizer          = localizer;
        _logger             = logger;
    }

    public async Task<BaseResponse<PagedInboxResult>> Handle(
        ListMyInboxQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId is null)
                return Unauthorized<PagedInboxResult>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var take = Math.Clamp(request.Take, 1, 100);
            var skip = Math.Max(0, request.Skip);

            var result = await _inboxService.ListInboxAsync(userId.Value, skip, take, cancellationToken);

            return Success(result, _localizer[SharedResourcesKey.InboxRetrievedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListMyInboxQuery");
            return ServerError<PagedInboxResult>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }

    private BaseResponse<T> Success<T>(T entity, string message) => new()
    {
        StatusCode = System.Net.HttpStatusCode.OK,
        Successed  = true,
        Data       = entity,
        Message    = message,
    };
}

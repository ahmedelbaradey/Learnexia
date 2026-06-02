using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Application.Features.Reengagement.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Queries.ListMyInbox;

/// <summary>
/// Returns the authenticated user's in-app notification inbox (P4-09 B4-4 / AC4).
/// Self-scoped: RecipientExternalUserId = currentUser — no IDOR surface.
/// Paginated, newest-first, includes all categories so the bell icon can render any type.
/// </summary>
public sealed class ListMyInboxQueryHandler
    : BaseResponseHandler,
      IQueryHandler<ListMyInboxQuery, BaseResponse<PagedInboxResult>>
{
    private readonly INotificationsDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public ListMyInboxQueryHandler(
        INotificationsDbContext db,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _db                 = db;
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

            var baseQuery = _db.Notifications
                .AsNoTracking()
                .Where(n => n.RecipientExternalUserId == userId.Value);

            var total = await baseQuery.CountAsync(cancellationToken);

            var items = await baseQuery
                .OrderByDescending(n => n.CreatedAtUtc)
                .Skip(skip)
                .Take(take)
                .Select(n => new InboxItemDto
                {
                    Id           = n.Id,
                    Title        = n.Title,
                    Body         = n.Body,
                    Category     = n.Category,
                    Code         = n.Code,
                    Data         = n.Data,
                    IsRead       = n.IsRead,
                    CreatedAtUtc = n.CreatedAtUtc,
                    OpenedAtUtc  = n.ReadAtUtc,
                })
                .ToListAsync(cancellationToken);

            var result = new PagedInboxResult
            {
                Items      = items,
                TotalCount = total,
                Skip       = skip,
                Take       = take,
            };

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

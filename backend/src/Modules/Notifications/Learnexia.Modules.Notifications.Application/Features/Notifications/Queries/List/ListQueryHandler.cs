using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Application.Features.Notifications.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Notifications.Application.Features.Notifications.Queries.List;

public sealed class ListQueryHandler
    : BaseResponseHandler, IQueryHandler<ListQuery, BaseResponse<List<NotificationResponse>>>
{
    private readonly INotificationsDbContext _db;
    private readonly ILoggerManager _logger;

    public ListQueryHandler(INotificationsDbContext db, ILoggerManager logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<BaseResponse<List<NotificationResponse>>> Handle(ListQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var items = await _db.Notifications
                .AsNoTracking()
                .Where(n => n.RecipientExternalUserId == request.RecipientUserId)
                .OrderByDescending(n => n.CreatedAtUtc)
                .Select(n => new NotificationResponse
                {
                    Id = n.Id,
                    Title = n.Title,
                    Body = n.Body,
                    Type = n.Type,
                    IsRead = n.IsRead,
                    CreatedAtUtc = n.CreatedAtUtc,
                })
                .ToListAsync(cancellationToken);

            if (items.Count == 0)
                return EmptyCollection(new List<NotificationResponse>());

            return Success(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListQuery");
            // Do not surface ex.Message to the caller (info disclosure, audit P4-01 #3); it is logged above.
            return ServerError<List<NotificationResponse>>();
        }
    }
}

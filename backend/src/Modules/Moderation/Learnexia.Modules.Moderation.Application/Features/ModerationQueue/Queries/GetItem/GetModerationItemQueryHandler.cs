using Learnexia.Modules.Moderation.Application.Abstractions;
using Learnexia.Modules.Moderation.Application.Features.ModerationQueue.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Moderation.Application.Features.ModerationQueue.Queries.GetItem;

/// <summary>
/// Handles <see cref="GetModerationItemQuery"/>. Delegates to <see cref="IModerationQueryService"/>
/// (Option C: no DbContext or EF types in this handler).
/// </summary>
public class GetModerationItemQueryHandler
    : BaseResponseHandler, IQueryHandler<GetModerationItemQuery, BaseResponse<ModerationItemDetailDto>>
{
    private readonly IModerationQueryService _queryService;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetModerationItemQueryHandler(
        IModerationQueryService queryService,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _queryService = queryService;
        _logger       = logger;
        _localizer    = localizer;
    }

    public async Task<BaseResponse<ModerationItemDetailDto>> Handle(
        GetModerationItemQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _queryService.GetByIdAsync(request.Id, cancellationToken);

            if (item is null)
                return NotFound<ModerationItemDetailDto>(_localizer[SharedResourcesKey.ModerationItemNotFound]);

            return Success(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error: in GetModerationItemQuery (Id={request.Id})");
            return ServerError<ModerationItemDetailDto>();
        }
    }
}

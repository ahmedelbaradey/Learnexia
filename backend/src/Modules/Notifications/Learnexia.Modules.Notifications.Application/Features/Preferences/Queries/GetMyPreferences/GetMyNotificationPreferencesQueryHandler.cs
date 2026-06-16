using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Application.Features.Preferences.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Notifications.Application.Features.Preferences.Queries.GetMyPreferences;

/// <summary>
/// Loads the caller's notification preferences (P2-12 BE-1).
/// Self-scoped: UserId is always resolved from the JWT via <see cref="ICurrentUserService"/> — never
/// from a parameter. When no rows exist yet the service returns defaults for the 4 user-visible categories
/// (no 404, nothing persisted on read). Queries are NOT auto-validated (CONVENTIONS §4).
/// Default synthesis and EF access live in <see cref="INotificationPreferenceService"/> (Option-C rule).
/// </summary>
public sealed class GetMyNotificationPreferencesQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetMyNotificationPreferencesQuery, BaseResponse<NotificationPreferencesResponse>>
{
    private readonly INotificationPreferenceService _preferenceService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public GetMyNotificationPreferencesQueryHandler(
        INotificationPreferenceService preferenceService,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _preferenceService  = preferenceService;
        _currentUserService = currentUserService;
        _localizer          = localizer;
        _logger             = logger;
    }

    public async Task<BaseResponse<NotificationPreferencesResponse>> Handle(
        GetMyNotificationPreferencesQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId is null)
                return Unauthorized<NotificationPreferencesResponse>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var response = await _preferenceService.GetPreferencesAsync(userId.Value, cancellationToken);

            return Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetMyNotificationPreferencesQuery");
            return ServerError<NotificationPreferencesResponse>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

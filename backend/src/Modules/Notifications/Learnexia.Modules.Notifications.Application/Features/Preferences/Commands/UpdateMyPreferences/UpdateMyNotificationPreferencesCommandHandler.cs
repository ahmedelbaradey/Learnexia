using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Notifications.Application.Features.Preferences.Commands.UpdateMyPreferences;

/// <summary>
/// Upserts notification preferences (P2-12 BE-1). One row per (UserId, Category); the full set of
/// categories in the request is written in a single explicit transaction (no Unit of Work, ADR 0001).
/// Self-scoped: UserId from JWT only; no body parameter is used or trusted for identity.
/// Transaction boundary lives inside <see cref="INotificationPreferenceService"/> (Option-C rule).
/// </summary>
public sealed class UpdateMyNotificationPreferencesCommandHandler
    : BaseResponseHandler, ICommandHandler<UpdateMyNotificationPreferencesCommand, BaseResponse<string>>
{
    private readonly INotificationPreferenceService _preferenceService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public UpdateMyNotificationPreferencesCommandHandler(
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

    public async Task<BaseResponse<string>> Handle(
        UpdateMyNotificationPreferencesCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId is null)
                return Unauthorized<string>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            await _preferenceService.UpsertPreferencesAsync(userId.Value, request.Preferences, cancellationToken);

            return Success<string>(_localizer[SharedResourcesKey.NotificationPreferencesUpdatedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in UpdateMyNotificationPreferencesCommand");
            return ServerError<string>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

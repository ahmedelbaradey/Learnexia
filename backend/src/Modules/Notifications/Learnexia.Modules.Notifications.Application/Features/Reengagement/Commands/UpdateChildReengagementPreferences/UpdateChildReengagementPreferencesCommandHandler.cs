using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Commands.UpdateChildReengagementPreferences;

/// <summary>
/// Upserts the parent's per-child re-engagement preferences (P4-09 B4-3 / AC3).
/// Writes all 3 schedulable categories in an explicit transaction (no Unit of Work, ADR 0001).
/// Transaction boundary lives inside <see cref="IChildReengagementPreferenceService"/> (Option-C rule).
/// Anti-IDOR / anti-enumeration: generic Forbidden regardless of whether the child exists or the
/// link doesn't — same message as the read endpoint.
/// </summary>
public sealed class UpdateChildReengagementPreferencesCommandHandler
    : BaseResponseHandler,
      ICommandHandler<UpdateChildReengagementPreferencesCommand, BaseResponse<string>>
{
    private readonly IChildReengagementPreferenceService _preferenceService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public UpdateChildReengagementPreferencesCommandHandler(
        IChildReengagementPreferenceService preferenceService,
        ICurrentUserService currentUserService,
        IParentChildQuery parentChildQuery,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _preferenceService  = preferenceService;
        _currentUserService = currentUserService;
        _parentChildQuery   = parentChildQuery;
        _localizer          = localizer;
        _logger             = logger;
    }

    public async Task<BaseResponse<string>> Handle(
        UpdateChildReengagementPreferencesCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var parentId = _currentUserService.UserId;
            if (parentId is null)
                return Unauthorized<string>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            // Anti-IDOR: same generic Forbidden regardless of child-exists / link-exists.
            var isParent = await _parentChildQuery.IsParentOfChildAsync(parentId.Value, request.ChildId, cancellationToken);
            if (!isParent)
                return Forbidden<string>(_localizer[SharedResourcesKey.NotAuthorizedForChild]);

            await _preferenceService.UpsertAsync(
                parentId.Value,
                request.ChildId,
                request.Items ?? [],
                request.QuietHoursStartLocal,
                request.QuietHoursEndLocal,
                request.TimeZoneId,
                request.DailyCap,
                cancellationToken);

            return Success<string>(_localizer[SharedResourcesKey.ChildPreferencesUpdatedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in UpdateChildReengagementPreferencesCommand");
            return ServerError<string>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

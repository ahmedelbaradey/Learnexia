using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Application.Features.Reengagement.Dtos;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Queries.GetChildReengagementPreferences;

/// <summary>
/// Loads (and synthesises defaults for) the parent's per-child re-engagement preferences (P4-09 B4-3 / AC3).
/// Default synthesis and EF access live in <see cref="IChildReengagementPreferenceService"/> (Option-C rule).
/// Anti-IDOR / anti-enumeration: any failure of the parent-child link check returns the same
/// generic Forbidden message regardless of whether the child exists.
/// </summary>
public sealed class GetChildReengagementPreferencesQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetChildReengagementPreferencesQuery, BaseResponse<List<ChildReengagementPreferenceDto>>>
{
    private readonly IChildReengagementPreferenceService _preferenceService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public GetChildReengagementPreferencesQueryHandler(
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

    public async Task<BaseResponse<List<ChildReengagementPreferenceDto>>> Handle(
        GetChildReengagementPreferencesQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var parentId = _currentUserService.UserId;
            if (parentId is null)
                return Unauthorized<List<ChildReengagementPreferenceDto>>(
                    _localizer[SharedResourcesKey.UnauthorizedAccess]);

            // Anti-IDOR: same generic Forbidden regardless of child-exists / link-exists (AC3).
            var isParent = await _parentChildQuery.IsParentOfChildAsync(parentId.Value, request.ChildId, cancellationToken);
            if (!isParent)
                return Forbidden<List<ChildReengagementPreferenceDto>>(
                    _localizer[SharedResourcesKey.NotAuthorizedForChild]);

            var result = await _preferenceService.GetForChildAsync(parentId.Value, request.ChildId, cancellationToken);

            return Success(result, _localizer[SharedResourcesKey.ChildPreferencesRetrievedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetChildReengagementPreferencesQuery");
            return ServerError<List<ChildReengagementPreferenceDto>>(
                _localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }

    // Override Success to accept a localised message (mirrors existing handler pattern).
    private BaseResponse<T> Success<T>(T entity, string message) => new()
    {
        StatusCode = System.Net.HttpStatusCode.OK,
        Successed  = true,
        Data       = entity,
        Message    = message,
    };
}

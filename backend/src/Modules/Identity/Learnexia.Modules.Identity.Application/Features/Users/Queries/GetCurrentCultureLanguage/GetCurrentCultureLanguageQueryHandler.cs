using System.Globalization;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Features.Users.Queries.Responses;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.GetCurrentCultureLanguage;

public class GetCurrentCultureLanguageQueryHandler : BaseResponseHandler, IQueryHandler<GetCurrentCultureLanguageQuery, BaseResponse<GetCurrentCultureLanguageResponse>>
{
    private readonly IIdentityServiceManager _identityService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetCurrentCultureLanguageQueryHandler(
        IIdentityServiceManager identityService,
        ICurrentUserService currentUserService,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _identityService = identityService;
        _currentUserService = currentUserService;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<GetCurrentCultureLanguageResponse>> Handle(GetCurrentCultureLanguageQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInfo("Getting current culture language information");

            var currentUserId = _currentUserService.UserId;
            if (!currentUserId.HasValue)
            {
                _logger.LogWarn("No authenticated user found");
                return Unauthorized<GetCurrentCultureLanguageResponse>(_localizer[SharedResourcesKey.UnauthorizedAccess]);
            }

            var currentCulture = CultureInfo.CurrentCulture.Name;

            var user = await _identityService.UserManagmentService.FindByIdAsync(currentUserId.Value.ToString());
            if (user == null)
            {
                _logger.LogWarn($"User with ID {currentUserId.Value} not found");
                return NotFound<GetCurrentCultureLanguageResponse>(_localizer[SharedResourcesKey.UserNotFound]);
            }

            var response = new GetCurrentCultureLanguageResponse
            {
                CurrentCulture = currentCulture,
                PreferredLanguage = user.PreferredLanguage,
            };

            _logger.LogInfo($"Successfully retrieved culture language information for user {currentUserId.Value}");
            return Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current culture language information");
            return ServerError<GetCurrentCultureLanguageResponse>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

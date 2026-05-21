using System.IdentityModel.Tokens.Jwt;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Modules.Identity.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Commands.SignOut;

public class SignOutCommandHandler : BaseResponseHandler, ICommandHandler<SignOutCommand, BaseResponse<string>>
{
    private readonly IIdentityServiceManager _identityServiceManager;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ISessionManagementService _sessionManagementService;
    private readonly ILoggerManager _logger;

    public SignOutCommandHandler(
        IIdentityServiceManager identityServiceManager,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        ISessionManagementService sessionManagementService,
        ILoggerManager logger)
    {
        _identityServiceManager = identityServiceManager;
        _currentUserService = currentUserService;
        _localizer = localizer;
        _sessionManagementService = sessionManagementService;
        _logger = logger;
    }

    public async Task<BaseResponse<string>> Handle(SignOutCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId.GetValueOrDefault();
            if (userId == 0)
                return Unauthorized<string>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var user = await _identityServiceManager.UserManagmentService.FindByIdAsync(userId.ToString());
            if (user == null)
                return NotFound<string>(_localizer[SharedResourcesKey.UserNotFound]);

            await TerminateUserSessionsAsync(userId, request);

            var userClaims = await _identityServiceManager.UserManagmentService.GetClaimsAsync(user);
            var fcmClaims = userClaims.Where(c => c.Type == CustomClaimTypes.FCMWebToken).ToList();
            if (fcmClaims.Any())
                await _identityServiceManager.UserManagmentService.RemoveClaimsAsync(user, fcmClaims);

            await _identityServiceManager.UserManagmentService.UpdateSecurityStampAsync(user);

            return Success<string>(_localizer[SharedResourcesKey.LogoutSuccessful]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sign out");
            return ServerError<string>(_localizer[SharedResourcesKey.LogoutSystemError]);
        }
    }

    private async Task TerminateUserSessionsAsync(int userId, SignOutCommand request)
    {
        try
        {
            var sessionId = GetCurrentSessionId();
            if (!string.IsNullOrEmpty(sessionId))
            {
                var terminated = await _sessionManagementService.TerminateSessionAsync(sessionId, SessionTerminationReason.UserLogout);
                if (terminated)
                    _logger.LogInfo($"Terminated session {sessionId} for user {userId} during logout");
                else
                    _logger.LogWarn($"Failed to terminate session {sessionId} for user {userId} - session may not exist");
            }
            else
            {
                _logger.LogWarn($"No session ID provided or found in JWT token for user {userId} logout");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error terminating sessions for user {userId} during logout");
        }
    }

    private string? GetCurrentSessionId()
    {
        try
        {
            return _currentUserService.GetClaimValue(JwtRegisteredClaimNames.Jti);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not extract session ID from current user context");
            return null;
        }
    }
}

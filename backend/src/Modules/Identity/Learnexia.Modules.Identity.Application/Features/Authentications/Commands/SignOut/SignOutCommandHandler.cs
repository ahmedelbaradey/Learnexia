using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Modules.Identity.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Commands.SignOut;

public class SignOutCommandHandler : BaseResponseHandler, ICommandHandler<SignOutCommand, BaseResponse<string>>
{
    private readonly IIdentityServiceManager _identityServiceManager;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ISessionManagementService _sessionManagementService;
    private readonly IDistributedCache _distributedCache;
    private readonly ILoggerManager _logger;

    public SignOutCommandHandler(
        IIdentityServiceManager identityServiceManager,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        ISessionManagementService sessionManagementService,
        IDistributedCache distributedCache,
        ILoggerManager logger)
    {
        _identityServiceManager = identityServiceManager;
        _currentUserService = currentUserService;
        _localizer = localizer;
        _sessionManagementService = sessionManagementService;
        _distributedCache = distributedCache;
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

            // P1-02 (AC-3): the primary, load-bearing revocation. Delete the refresh-token cache entry
            // directly so it can never be exchanged again — this runs regardless of whether the session
            // record was resolved/terminated above (best-effort session termination is complementary).
            await RevokeRefreshTokenAsync(userId);

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

    private async Task RevokeRefreshTokenAsync(int userId)
    {
        try
        {
            await _distributedCache.RemoveAsync($"userrefreshtoken-{userId}");
            _logger.LogInfo($"Revoked refresh token for user {userId} during logout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error revoking refresh token for user {userId} during logout");
        }
    }

    private string? GetCurrentSessionId()
    {
        try
        {
            // P1-02 (AC-3): SessionManagementService stores sessions keyed by the "SessionId" GUID claim,
            // NOT the Jti. Reading Jti here looked up a non-existent key and silently no-op'd. Use the
            // "SessionId" claim so session termination targets the correct record.
            return _currentUserService.GetClaimValue("SessionId");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not extract session ID from current user context");
            return null;
        }
    }
}

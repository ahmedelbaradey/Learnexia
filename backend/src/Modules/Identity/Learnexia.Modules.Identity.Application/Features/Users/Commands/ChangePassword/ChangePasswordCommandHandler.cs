using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.ChangePassword;

/// <summary>
/// Handles self-service password change (P2-12 SEC-1). Validates the current password, changes it via
/// UserManager, then: (a) terminates all OTHER sessions (keeps current), and (b) revokes the refresh-token
/// cache entry so any outstanding tokens are immediately invalid. UserId is always from the JWT
/// (<see cref="ICurrentUserService"/>) — the command's <c>UserId</c> field is ignored for security.
/// </summary>
public class SetNewPasswordCommandHandler : BaseResponseHandler, ICommandHandler<ChangePasswordCommand, BaseResponse<string>>
{
    private readonly IIdentityServiceManager _identityServiceManager;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISessionManagementService _sessionManagementService;
    private readonly IDistributedCache _distributedCache;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public SetNewPasswordCommandHandler(
        IIdentityServiceManager identityServiceManager,
        ICurrentUserService currentUserService,
        ISessionManagementService sessionManagementService,
        IDistributedCache distributedCache,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _identityServiceManager = identityServiceManager;
        _currentUserService = currentUserService;
        _sessionManagementService = sessionManagementService;
        _distributedCache = distributedCache;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<BaseResponse<string>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // UserId always from JWT — never trust the command's UserId field (self-scope).
            var userId = _currentUserService.UserId.GetValueOrDefault();
            if (userId == 0)
                return Unauthorized<string>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var user = await _identityServiceManager.UserManagmentService.FindByIdAsync(userId.ToString());
            if (user == null)
                return NotFound<string>(_localizer[SharedResourcesKey.UserNotFound]);

            if (string.IsNullOrEmpty(request.CurrentPassword))
                return BadRequest<string>(_localizer[SharedResourcesKey.PasswordIncorrectCurrent]);

            var result = await _identityServiceManager.AuthenticationService.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError(null, $"ChangePassword failed for user {userId}: {errors}");
                return BadRequest<string>(_localizer[SharedResourcesKey.PasswordChangeSystemError]);
            }

            // P2-12 SEC-1: after a successful password change, invalidate all OTHER sessions (keep current),
            // and revoke the refresh-token cache so no outstanding token can be exchanged again.
            await InvalidateOtherSessionsAsync(userId);
            await RevokeRefreshTokenAsync(userId);

            return Success<string>(_localizer[SharedResourcesKey.PasswordChangedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ChangePasswordCommand");
            return ServerError<string>(_localizer[SharedResourcesKey.PasswordChangeSystemError]);
        }
    }

    /// <summary>
    /// Terminates all sessions for this user EXCEPT the current one (identified by the "SessionId" JWT
    /// claim), mirroring the pattern used in <c>SignOutCommandHandler</c>.
    /// </summary>
    private async Task InvalidateOtherSessionsAsync(int userId)
    {
        try
        {
            var currentSessionId = _currentUserService.GetClaimValue("SessionId");
            var sessions = await _sessionManagementService.GetUserSessionsAsync(userId);

            foreach (var session in sessions)
            {
                // Skip the caller's own session — they should stay logged in after changing their password.
                if (session.SessionId == currentSessionId)
                    continue;

                var terminated = await _sessionManagementService.TerminateSessionAsync(
                    session.SessionId, SessionTerminationReason.SecurityRevocation);

                if (terminated)
                    _logger.LogInfo($"Terminated session {session.SessionId} for user {userId} after password change.");
                else
                    _logger.LogWarn($"Could not terminate session {session.SessionId} for user {userId} after password change.");
            }
        }
        catch (Exception ex)
        {
            // Best-effort: do not abort the password change if session cleanup fails.
            _logger.LogError(ex, $"Error terminating other sessions for user {userId} after password change.");
        }
    }

    /// <summary>
    /// Revokes the refresh-token cache entry so any outstanding refresh token is immediately invalid,
    /// mirroring the approach used in <c>SignOutCommandHandler</c>.
    /// </summary>
    private async Task RevokeRefreshTokenAsync(int userId)
    {
        try
        {
            await _distributedCache.RemoveAsync($"userrefreshtoken-{userId}");
            _logger.LogInfo($"Revoked refresh token for user {userId} after password change.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error revoking refresh token for user {userId} after password change.");
        }
    }
}

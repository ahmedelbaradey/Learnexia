using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Account.Commands.SignOutOthers;

/// <summary>
/// Terminates all of the caller's sessions EXCEPT the current one (P2-12 SEC-2). Does NOT revoke the
/// refresh token so the caller stays logged in on the current device. Mirrors the session-termination
/// logic in <c>SignOutCommandHandler</c>. Self-scoped: UserId and SessionId are from the JWT only.
/// </summary>
public sealed class SignOutOthersCommandHandler
    : BaseResponseHandler, ICommandHandler<SignOutOthersCommand, BaseResponse<string>>
{
    private readonly ISessionManagementService _sessionManagementService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public SignOutOthersCommandHandler(
        ISessionManagementService sessionManagementService,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _sessionManagementService = sessionManagementService;
        _currentUserService = currentUserService;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<BaseResponse<string>> Handle(SignOutOthersCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId is null)
                return Unauthorized<string>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            // The current session is identified by the "SessionId" JWT claim (mirrors SignOutCommandHandler).
            var currentSessionId = _currentUserService.GetClaimValue("SessionId");

            var sessions = await _sessionManagementService.GetUserSessionsAsync(userId.Value);

            foreach (var session in sessions)
            {
                // Keep the caller's own session alive.
                if (session.SessionId == currentSessionId)
                    continue;

                var terminated = await _sessionManagementService.TerminateSessionAsync(
                    session.SessionId, SessionTerminationReason.SecurityRevocation);

                if (terminated)
                    _logger.LogInfo($"Terminated other session {session.SessionId} for user {userId.Value}.");
                else
                    _logger.LogWarn($"Could not terminate session {session.SessionId} for user {userId.Value}.");
            }

            return Success<string>(_localizer[SharedResourcesKey.OtherSessionsSignedOutSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in SignOutOthersCommand");
            return ServerError<string>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Enums;
using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Commands.RefreshToken;

public class RefreshTokenCommandHandler : BaseResponseHandler, ICommandHandler<RefreshTokenCommand, BaseResponse<JwtAuthResponse>>
{
    private readonly IIdentityServiceManager _service;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public RefreshTokenCommandHandler(
        IIdentityServiceManager service,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _service = service;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<BaseResponse<JwtAuthResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var jwtToken = _service.AuthenticationService.ReadJwtToken(request.AccessToken);
            var userIdAndExpiryDate = await _service.AuthenticationService.ValidateDetails(jwtToken, request.AccessToken, request.RefreshToken);

            switch (userIdAndExpiryDate)
            {
                // P1-02 (AC-4): auth failures must be 401, not 500 — no internal-state leakage and the
                // client knows to re-login. AlgorithmIsWrong / not-found / expired are all auth failures.
                case ("AlgorithmIsWrong", null):
                    return Unauthorized<JwtAuthResponse>("Algorithm is wrong.");
                case ("RefreshTokenNotFound", null):
                    return Unauthorized<JwtAuthResponse>("Refresh token not found.");
                case ("RefreshTokenIsExpired", null):
                    return Unauthorized<JwtAuthResponse>("Refresh token is expired.");

                // TokenIsRunning = the access token is still valid; this is a caller logic error
                // (refresh too early), not an auth failure, so it stays a 400 BadRequest.
                case ("TokenIsRunning", null):
                    return BadRequest<JwtAuthResponse>("Token is not expired.");
            }

            var (userId, expiryDate) = userIdAndExpiryDate;

            var user = await _service.UserManagmentService.FindByIdAsync(userId);
            if (user == null)
                return NotFound<JwtAuthResponse>("User not found!");

            // P7-07 security fix: a suspended or deleted account whose Redis refresh token was
            // not revoked (e.g. revocation failed best-effort, or token was issued before deletion)
            // must not be able to mint a new access token. Mirrors SignInCommandHandler ~lines 64-70:
            // both IsActive and AccountStatus are checked; the same generic "deactivated" locale
            // key is returned so no state is leaked to the caller.
            if (!user.IsActive || user.AccountStatus is AccountStatus.Suspended or AccountStatus.Deleted)
            {
                _logger.LogWarn($"RefreshTokenCommandHandler: token refresh denied for user {userId} — IsActive={user.IsActive}, AccountStatus={user.AccountStatus}.");
                return Unauthorized<JwtAuthResponse>(_localizer[SharedResourcesKey.LoginAccountDeactivated]);
            }

            var result = await _service.AuthenticationService.GetRefreshToken(user, jwtToken, expiryDate, request.RefreshToken);
            return Success(result);
        }
        catch (Exception ex)
        {
            // Never surface ex.Message to the caller (info disclosure). Log server-side and
            // return a generic localized error, mirroring the delete/login handlers.
            _logger.LogError(ex, $"RefreshTokenCommandHandler: unexpected error during token refresh — {ex.Message}");
            return ServerError<JwtAuthResponse>(_localizer[SharedResourcesKey.LoginSystemError]);
        }
    }
}

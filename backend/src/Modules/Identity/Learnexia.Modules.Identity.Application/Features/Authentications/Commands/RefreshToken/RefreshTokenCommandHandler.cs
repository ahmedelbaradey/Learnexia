using System.IdentityModel.Tokens.Jwt;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Features.Authentications.Commands.SignIn;
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
    private readonly ISessionManagementService _sessionManagementService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public RefreshTokenCommandHandler(
        IIdentityServiceManager service,
        ISessionManagementService sessionManagementService,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _service = service;
        _sessionManagementService = sessionManagementService;
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

            // P6-07 BE-1: GetRefreshToken mints a brand-new JWT with a new SessionId claim (a fresh GUID
            // from GetClaims). Without creating a session here, OnTokenValidated would reject the newly
            // issued token immediately. Thread the claim's SessionId into session creation — same pattern
            // as SignInCommandHandler / RegisterParentCommandHandler / GoogleSignInCommandHandler.
            var sessionInfo = ExtractSessionInfoFromToken(result.AccessToken);
            if (sessionInfo != null && int.TryParse(userId, out var userIdInt))
            {
                try
                {
                    var session = await _sessionManagementService.CreateSessionAsync(userIdInt, sessionInfo.JwtId, sessionInfo.SessionId);
                    result.SessionId = session.SessionId;
                }
                catch (Exception ex)
                {
                    // Session creation failed. The issued token's first authenticated call will be rejected by
                    // OnTokenValidated because no session is persisted under its SessionId claim. Log loudly.
                    _logger.LogError(ex, $"RefreshToken: failed to create session for user {userId}. The refreshed token will be invalid on first authenticated call.");
                }
            }

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

    private static SessionExtractionInfo? ExtractSessionInfoFromToken(string? accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
            return null;

        try
        {
            var jwtHandler = new JwtSecurityTokenHandler();
            if (!jwtHandler.CanReadToken(accessToken))
                return null;

            var jwtToken = jwtHandler.ReadJwtToken(accessToken);
            var jwtIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
            var sessionIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "SessionId");

            if (jwtIdClaim == null || sessionIdClaim == null)
                return null;

            return new SessionExtractionInfo
            {
                JwtId = jwtIdClaim.Value,
                SessionId = sessionIdClaim.Value,
            };
        }
        catch
        {
            return null;
        }
    }
}

using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Commands.RefreshToken;

public class RefreshTokenCommandHandler : BaseResponseHandler, ICommandHandler<RefreshTokenCommand, BaseResponse<JwtAuthResponse>>
{
    private readonly IIdentityServiceManager _service;

    public RefreshTokenCommandHandler(IIdentityServiceManager service)
    {
        _service = service;
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

            var result = await _service.AuthenticationService.GetRefreshToken(user, jwtToken, expiryDate, request.RefreshToken);
            return Success(result);
        }
        catch (Exception ex)
        {
            return ServerError<JwtAuthResponse>(ex.Message);
        }
    }
}

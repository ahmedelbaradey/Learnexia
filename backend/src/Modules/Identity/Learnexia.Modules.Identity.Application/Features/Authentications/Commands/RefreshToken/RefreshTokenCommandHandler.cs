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
                case ("AlgorithmIsWrong", null):
                    return ServerError<JwtAuthResponse>("Algorithm is wrong.");
                case ("TokenIsRunning", null):
                    return ServerError<JwtAuthResponse>("Toekn is not expired.");
                case ("RefreshTokenNotFound", null):
                    return ServerError<JwtAuthResponse>("Refresh token not found.");
                case ("RefreshTokenIsExpired", null):
                    return ServerError<JwtAuthResponse>("Refresh token is expired.");
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

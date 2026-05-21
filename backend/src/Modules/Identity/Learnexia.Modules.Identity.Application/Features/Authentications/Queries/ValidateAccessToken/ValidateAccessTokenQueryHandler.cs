using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Queries.ValidateAccessToken;

public class ValidateAccessTokenQueryHandler : BaseResponseHandler, IQueryHandler<AccessTokenQuery, BaseResponse<string>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ILoggerManager _logger;

    public ValidateAccessTokenQueryHandler(IIdentityServiceManager service, ILoggerManager logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task<BaseResponse<string>> Handle(AccessTokenQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.AuthenticationService.ValidateJwtToken(request.Accesstoken);
            if (result == "NotExpired")
                return Success("this token is not expired.");

            return Unauthorized<string>("this token is expired.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating access token");
            return ServerError<string>(ex.Message);
        }
    }
}

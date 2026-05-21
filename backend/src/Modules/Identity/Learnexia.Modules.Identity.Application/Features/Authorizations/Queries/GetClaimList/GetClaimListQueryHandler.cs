using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Authorizations.Queries.GetClaimList;

public class GetClaimListQueryHandler : BaseResponseHandler, IQueryHandler<GetClaimListQuery, BaseResponse<List<RoleClaims>>>
{
    private readonly ILoggerManager _logger;

    public GetClaimListQueryHandler(ILoggerManager logger)
    {
        _logger = logger;
    }

    public Task<BaseResponse<List<RoleClaims>>> Handle(GetClaimListQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var claims = new List<RoleClaims>();
            foreach (var module in Claims.GenerateModules())
            {
                foreach (var permission in Claims.GeneratePermissions())
                {
                    claims.Add(new RoleClaims
                    {
                        Value = $"{module}.{permission}",
                        Type = "Permission",
                        HasClaim = false,
                    });
                }
            }

            return Task.FromResult(Success(claims));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            return Task.FromResult(ServerError<List<RoleClaims>>(ex.Message));
        }
    }
}

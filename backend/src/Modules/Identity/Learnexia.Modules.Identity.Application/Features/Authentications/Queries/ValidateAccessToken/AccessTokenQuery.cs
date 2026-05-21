using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Queries.ValidateAccessToken;

public record AccessTokenQuery : IQuery<BaseResponse<string>>
{
    public string Accesstoken { get; set; } = null!;
}

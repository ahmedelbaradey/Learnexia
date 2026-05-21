using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Commands.RefreshToken;

public record RefreshTokenCommand : ICommand<BaseResponse<JwtAuthResponse>>
{
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
}

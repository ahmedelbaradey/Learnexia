using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Commands.SignIn;

public record SignInCommand : ICommand<BaseResponse<JwtAuthResponse>>
{
    public string UserName { get; set; } = null!;
    public string Password { get; set; } = null!;
}

public record SessionExtractionInfo
{
    public string JwtId { get; set; } = null!;
    public string SessionId { get; set; } = null!;
}

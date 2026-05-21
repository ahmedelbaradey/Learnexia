using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Commands.SignOut;

public record SignOutCommand : ICommand<BaseResponse<string>>
{
}

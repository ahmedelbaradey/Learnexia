using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.AdminResetPassword;

public record AdminResetPasswordCommand : ICommand<BaseResponse<string>>
{
    public int UserId { get; set; }
}

using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.DeleteUser;

public record DeleteUserCommand : ICommand<BaseResponse<string>>
{
    public int Id { get; set; }
}

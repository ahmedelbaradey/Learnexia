using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Authorizations.Commands.DeleteRole;

public record DeleteRoleCommand : ICommand<BaseResponse<string>>
{
    public int Id { get; set; }
}

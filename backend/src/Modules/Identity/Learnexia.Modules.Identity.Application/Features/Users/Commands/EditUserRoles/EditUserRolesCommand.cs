using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.EditUserRoles;

public record EditUserRolesCommand : EditUserRolesRequest, ICommand<BaseResponse<string>>
{
}

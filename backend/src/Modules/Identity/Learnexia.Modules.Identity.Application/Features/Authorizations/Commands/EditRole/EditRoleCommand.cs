using Learnexia.Modules.Identity.Application.Features.Authorizations.Queries.Responses;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Authorizations.Commands.EditRole;

public record EditRoleCommand : GetRoleByIdResponse, ICommand<BaseResponse<string>>
{
}

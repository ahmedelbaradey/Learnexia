using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Authorizations.Commands.AddRole;

public record AddRoleCommand : ICommand<BaseResponse<string>>
{
    public string roleName { get; set; } = null!;
    public List<RoleClaims> RoleClaims { get; set; } = null!;
}

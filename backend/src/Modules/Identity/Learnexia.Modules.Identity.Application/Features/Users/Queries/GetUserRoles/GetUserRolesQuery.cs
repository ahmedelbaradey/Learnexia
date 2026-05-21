using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.GetUserRoles;

public record GetUserRolesQuery : IQuery<BaseResponse<ManageUserRolesResponse>>
{
    public int Id { get; set; }
}

using Learnexia.Modules.Identity.Application.Features.Users.Queries.Responses;
using Learnexia.Shared.Kernel.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.GetUsersByRole;

public record GetUsersByRoleQuery : BaseListDto, IQuery<PaginatedResult<GetUserListResponse>>
{
    public string RoleName { get; set; } = null!;
}

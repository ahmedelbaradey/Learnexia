using Learnexia.Modules.Identity.Application.Features.Users.Queries.Responses;
using Learnexia.Shared.Kernel.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.List;

public record ListQuery : BaseListDto, IQuery<PaginatedResult<GetUserListResponse>>
{
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
    public string? Name { get; set; }
}

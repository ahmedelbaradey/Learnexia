using Learnexia.Modules.Identity.Application.Features.Authorizations.Queries.Responses;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Authorizations.Queries.GetRoleList;

public record GetRoleListQuery : IQuery<BaseResponse<List<GetRoleListResponse>>>
{
}

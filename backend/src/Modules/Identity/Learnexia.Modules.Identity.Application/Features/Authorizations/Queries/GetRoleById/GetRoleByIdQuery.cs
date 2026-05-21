using Learnexia.Modules.Identity.Application.Features.Authorizations.Queries.Responses;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Authorizations.Queries.GetRoleById;

public record GetRoleByIdQuery : IQuery<BaseResponse<GetRoleByIdResponse>>
{
    public int Id { get; set; }
}

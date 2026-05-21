using Learnexia.Modules.Identity.Application.Features.Users.Queries.Responses;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.Get;

public record GetUserQuery : IQuery<BaseResponse<GetUserResponse>>
{
    public int Id { get; set; }
}

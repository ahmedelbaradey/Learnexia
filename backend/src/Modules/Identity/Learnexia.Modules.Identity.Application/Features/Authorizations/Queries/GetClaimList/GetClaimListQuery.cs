using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Authorizations.Queries.GetClaimList;

public record GetClaimListQuery : IQuery<BaseResponse<List<RoleClaims>>>
{
}

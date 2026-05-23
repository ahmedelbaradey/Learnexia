using Learnexia.Modules.Identity.Application.Features.Account.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Account.Queries.GetMyProfile;

// Returns the CURRENT authenticated user's account profile (resolved server-side from the JWT in
// the handler). Queries are not auto-validated (CONVENTIONS §4); there is intentionally NO id
// parameter — the endpoint reads only ICurrentUserService, so there is no IDOR surface.
public record GetMyProfileQuery : IQuery<BaseResponse<AccountProfileResponse>>;

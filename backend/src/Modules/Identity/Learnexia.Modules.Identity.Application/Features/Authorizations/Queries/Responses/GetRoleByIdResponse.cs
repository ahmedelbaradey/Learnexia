using Learnexia.Modules.Identity.Domain.Helpers;

namespace Learnexia.Modules.Identity.Application.Features.Authorizations.Queries.Responses;

public record GetRoleByIdResponse : GetRoleListResponse
{
    public List<RoleClaims> RoleClaims { get; set; } = null!;
}

using Learnexia.Modules.Identity.Domain.Helpers;

namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.Responses;

public record GetUserResponse : GetUserListResponse
{
    public List<UserRoles> UserRoles { get; set; } = null!;
}

using Learnexia.Modules.Identity.Application.Features.Users.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.GetUserProfile;

public record GetUserProfileQuery : IQuery<BaseResponse<UserProfileResponseDto>>
{
    public int? UserId { get; set; }
}

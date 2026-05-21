using Learnexia.Modules.Identity.Application.Features.Users.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Http;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.UpdateUserProfile;

public record UpdateUserProfileCommand : BaseUserDto, ICommand<BaseResponse<string>>
{
    public IFormFile? CVFile { get; set; }
    public IFormFile? PersonalPhoto { get; set; }
}

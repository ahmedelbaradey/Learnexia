using Learnexia.Modules.Identity.Application.Features.Users.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.AddUser;

public record AddUserCommand : BaseUserDto, ICommand<BaseResponse<string>>
{
    public string? CVFile { get; set; }
    public string? PersonalPhoto { get; set; }
    public List<string> Roles { get; set; } = null!;
}

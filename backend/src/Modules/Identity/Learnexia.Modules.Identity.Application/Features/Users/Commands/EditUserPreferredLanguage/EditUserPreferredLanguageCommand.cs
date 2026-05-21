using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.EditUserPreferredLanguage;

public record EditUserPreferredLanguageCommand : ICommand<BaseResponse<string>>
{
    public int Id { get; set; }
    public string UserPreferredLanguage { get; set; } = null!;
}

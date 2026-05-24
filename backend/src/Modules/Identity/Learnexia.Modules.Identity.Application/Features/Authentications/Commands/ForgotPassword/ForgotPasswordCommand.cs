using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Commands.ForgotPassword;

// Anonymous "forgot password" request. Returns a generic success regardless of whether the email maps to
// an account (no enumeration). The string payload is a localized confirmation message only.
public record ForgotPasswordCommand : ICommand<BaseResponse<string>>
{
    public string Email { get; set; } = null!;
}

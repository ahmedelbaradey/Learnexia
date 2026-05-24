using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Commands.ResetPassword;

// Anonymous "set new password" using the single-use token from the reset email. On a bad email or a
// bad/expired token the handler returns ONE generic localized failure (no distinction → no enumeration).
public record ResetPasswordCommand : ICommand<BaseResponse<string>>
{
    public string Email { get; set; } = null!;
    public string Token { get; set; } = null!;
    public string NewPassword { get; set; } = null!;
}

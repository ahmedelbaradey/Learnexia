using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Commands.RegisterParent;

// Parent self-registration. Role is server-decided (always Parent) — there is intentionally NO
// Roles/Role field on this command, so an anonymous caller cannot inject a role or create a
// Student/child account through this path (AC-2, BE-5). FullName is optional; when omitted the
// handler defaults it to the email local-part.
public record RegisterParentCommand : ICommand<BaseResponse<JwtAuthResponse>>
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string? FullName { get; set; }
}

using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Account.Commands.UpdateMyPreferredLanguage;

// Self-scoped preferred-language update for authenticated parents (fix for the 403 that the
// admin-only EditUserPreferredLanguageCommand produced). The acting user is ALWAYS resolved
// from the JWT (ICurrentUserService) inside the handler — there is intentionally NO id/role
// field, so a user cannot edit another account's language and cannot escalate privileges
// (no mass-assignment, no IDOR). ICommand<> so ValidationBehavior runs (en/ar → 422).
public record UpdateMyPreferredLanguageCommand : ICommand<BaseResponse<string>>
{
    // Accepted values: "ar", "en", "ar-EG", "en-US" (validated by the companion validator).
    public string UserPreferredLanguage { get; set; } = null!;
}

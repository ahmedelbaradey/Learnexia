using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Commands.GoogleSignIn;

/// <summary>
/// Google social sign-in (P1-12 BE-5). The frontend obtains a Google ID token and POSTs it here;
/// we verify it, find-or-create the parent account, link the Google external login, and issue OUR
/// JWT/refresh token + session — at parity with the password sign-in path.
/// </summary>
public record GoogleSignInCommand : ICommand<BaseResponse<JwtAuthResponse>>
{
    public string IdToken { get; set; } = null!;
}

using Learnexia.Modules.Identity.Application.Features.Account.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Account.Commands.UpdateMyProfile;

// Self-update of the authenticated user's own account profile (P1-12 BE-1). The acting user is
// ALWAYS resolved from the JWT (ICurrentUserService) in the handler — there is intentionally NO
// id/email/role field, so a user cannot edit another account and cannot escalate privileges
// (no mass-assignment). ICommand<> so ValidationBehavior runs (en/ar localized messages → 422).
// Avatar is read-only here (set by the avatar-upload endpoint, BE-4) and is therefore not accepted.
public record UpdateMyProfileCommand : ICommand<BaseResponse<AccountProfileResponse>>
{
    public string FullName { get; set; } = null!;

    // Optional. Maps to the inherited Identity PhoneNumber.
    public string? Phone { get; set; }

    // Optional. Maps to Nationality.
    public string? Country { get; set; }
}

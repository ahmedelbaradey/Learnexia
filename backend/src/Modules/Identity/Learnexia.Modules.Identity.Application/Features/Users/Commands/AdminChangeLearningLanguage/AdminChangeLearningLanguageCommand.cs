using Learnexia.Modules.Identity.Application.Features.Users.Dtos.Admin;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.AdminChangeLearningLanguage;

/// <summary>
/// Admin command to change a child's <c>LearningLanguage</c> (medium of instruction) (P7-08 BE-6).
///
/// This is the DESTRUCTIVE path — mirrors the parent's <c>ChangeLearningLanguageCommand</c> (P8-04):
///   - Requires explicit confirmation (<c>ConfirmFreshStart = true</c>).
///   - A missing/false flag → HTTP 424 FailedDependency, no mutation.
///   - After confirm: calls <c>IChildAccountService.ChangeLearningLanguageAsync</c> (the seam).
///   - The seam commits the language change and publishes <c>LearningLanguageChangedIntegrationEvent</c>.
///   - The Learning consumer hard-deletes the child's Math/Science Attempt rows
///     (Arabic/English subject attempts and all gamification data are untouched).
///   - Same-language requests are a no-op (seam's built-in guard).
///
/// Differs from the parent path in authorization only:
///   - Parent path: JWT parent id + IsLinkedAsync (family-scope IDOR).
///   - Admin path: class-level AdminOnly policy on AdminUsersController — admin acts on any child
///     without a family-link restriction (product decision: admin is a support override).
///
/// Learning-language claim staleness note (Q-C3): <c>learning_language</c> is a JWT claim.
/// The new value takes effect on the child's NEXT token (refresh/sign-in); the issued access
/// token retains the old value until it expires. This is the same bounded-staleness behaviour as
/// the parent P8-04 path (stateless JWT, no live rewrite — documented G2 follow-up).
/// </summary>
public record AdminChangeLearningLanguageCommand : ICommand<BaseResponse<AdminChangedLearningLanguageResponseDto>>
{
    public int ChildId { get; init; }

    /// <summary>Target learning language. Accepted values: "ar" | "en".</summary>
    public string LearningLanguage { get; init; } = null!;

    /// <summary>
    /// Destructive-operation confirm gate. Must be <c>true</c> for the change to proceed.
    /// <c>false</c> or absent → HTTP 424 FailedDependency (no mutation, no event).
    /// </summary>
    public bool ConfirmFreshStart { get; init; }
}

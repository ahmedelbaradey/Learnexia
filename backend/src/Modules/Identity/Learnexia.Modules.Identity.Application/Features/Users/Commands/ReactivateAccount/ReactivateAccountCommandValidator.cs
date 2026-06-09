using FluentValidation;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.ReactivateAccount;

/// <summary>
/// Validator for <see cref="ReactivateAccountCommand"/> (P7-07 BE-3).
/// Reason is optional but bounded if supplied.
/// </summary>
public class ReactivateAccountCommandValidator : AbstractValidator<ReactivateAccountCommand>
{
    public ReactivateAccountCommandValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0)
            .WithMessage(SharedResourcesKey.AccountLifecycleUserIdRequired);

        // Reason is optional; if supplied it must not exceed 500 chars.
        When(x => x.Reason != null, () =>
        {
            RuleFor(x => x.Reason)
                .MaximumLength(500)
                .WithMessage(SharedResourcesKey.AccountLifecycleReasonTooLong);
        });
    }
}

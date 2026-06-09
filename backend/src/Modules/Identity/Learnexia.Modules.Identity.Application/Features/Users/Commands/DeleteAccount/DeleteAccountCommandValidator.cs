using FluentValidation;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.DeleteAccount;

/// <summary>
/// Validator for <see cref="DeleteAccountCommand"/> (P7-07 BE-4).
/// Note: the Confirm = false guard is intentionally NOT checked here because FluentValidation
/// returns HTTP 422 (UnprocessableEntity), whereas the spec requires HTTP 424 (FailedDependency)
/// for the unconfirmed-delete scenario. The confirm gate is enforced in the handler instead
/// (mirrors the P8-04 pattern in ChangeLearningLanguageCommandHandler).
/// </summary>
public class DeleteAccountCommandValidator : AbstractValidator<DeleteAccountCommand>
{
    public DeleteAccountCommandValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0)
            .WithMessage(SharedResourcesKey.AccountLifecycleUserIdRequired);

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage(SharedResourcesKey.AccountLifecycleReasonRequired)
            .MaximumLength(500)
            .WithMessage(SharedResourcesKey.AccountLifecycleReasonTooLong);
    }
}

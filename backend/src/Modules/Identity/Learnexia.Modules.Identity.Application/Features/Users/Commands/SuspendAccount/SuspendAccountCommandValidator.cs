using FluentValidation;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.SuspendAccount;

/// <summary>
/// Validator for <see cref="SuspendAccountCommand"/> (P7-07 BE-2).
/// ICommand validators are picked up automatically by ValidationBehavior.
/// </summary>
public class SuspendAccountCommandValidator : AbstractValidator<SuspendAccountCommand>
{
    public SuspendAccountCommandValidator()
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

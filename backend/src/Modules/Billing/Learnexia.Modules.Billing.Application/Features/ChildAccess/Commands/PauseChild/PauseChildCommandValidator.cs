using FluentValidation;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.ChildAccess.Commands.PauseChild;

/// <summary>
/// FluentValidation validator for <see cref="PauseChildCommand"/> (P10-18-BE-6).
///
/// <para><c>parentUserId</c> is resolved from the JWT inside the handler — it is NOT a property
/// of the command record and therefore requires no rule here.</para>
/// </summary>
public sealed class PauseChildCommandValidator : AbstractValidator<PauseChildCommand>
{
    public PauseChildCommandValidator()
    {
        RuleFor(x => x.ChildId)
            .GreaterThan(0)
            .WithMessage(SharedResourcesKey.EmptyIdValidation);
    }
}

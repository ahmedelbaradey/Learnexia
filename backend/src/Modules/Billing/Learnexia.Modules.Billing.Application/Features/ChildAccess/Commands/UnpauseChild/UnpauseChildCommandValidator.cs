using FluentValidation;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.ChildAccess.Commands.UnpauseChild;

/// <summary>
/// FluentValidation validator for <see cref="UnpauseChildCommand"/> (P10-18-BE-6).
/// </summary>
public sealed class UnpauseChildCommandValidator : AbstractValidator<UnpauseChildCommand>
{
    public UnpauseChildCommandValidator()
    {
        RuleFor(x => x.ChildId)
            .GreaterThan(0)
            .WithMessage(SharedResourcesKey.EmptyIdValidation);
    }
}

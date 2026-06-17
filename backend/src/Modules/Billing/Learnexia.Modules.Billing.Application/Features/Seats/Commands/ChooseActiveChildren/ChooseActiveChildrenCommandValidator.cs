using FluentValidation;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Seats.Commands.ChooseActiveChildren;

/// <summary>
/// FluentValidation validator for <see cref="ChooseActiveChildrenCommand"/> (P10-15-BE-6).
///
/// <para>Validates the request structure. Business-rule limit (count ≤ paidActiveSeats) is enforced
/// inside the service (requires DB access and plan resolution).</para>
/// </summary>
public sealed class ChooseActiveChildrenCommandValidator
    : AbstractValidator<ChooseActiveChildrenCommand>
{
    public ChooseActiveChildrenCommandValidator()
    {
        RuleFor(x => x.ActiveChildIds)
            .NotNull()
            .WithMessage(SharedResourcesKey.Required)
            .Must(ids => ids is not null && ids.All(id => id > 0))
            .WithMessage(SharedResourcesKey.EmptyIdValidation);
    }
}

using FluentValidation;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Seats.Commands.ReactivateChildSeat;

/// <summary>
/// FluentValidation validator for <see cref="ReactivateChildSeatCommand"/> (P10-15-BE-6).
/// </summary>
public sealed class ReactivateChildSeatCommandValidator
    : AbstractValidator<ReactivateChildSeatCommand>
{
    public ReactivateChildSeatCommandValidator()
    {
        RuleFor(x => x.ChildId)
            .GreaterThan(0)
            .WithMessage(SharedResourcesKey.EmptyIdValidation);

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithMessage(SharedResourcesKey.Required)
            .MaximumLength(128)
            .WithMessage(SharedResourcesKey.MaxLength);
    }
}

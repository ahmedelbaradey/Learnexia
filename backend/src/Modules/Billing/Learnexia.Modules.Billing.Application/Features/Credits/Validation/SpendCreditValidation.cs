using FluentValidation;
using Learnexia.Modules.Billing.Application.Features.Credits.Commands.SpendCredit;

namespace Learnexia.Modules.Billing.Application.Features.Credits.Validation;

public class SpendCreditValidation : AbstractValidator<SpendCreditCommand>
{
    public SpendCreditValidation()
    {
        RuleFor(x => x.ChildId)
            .GreaterThan(0)
            .WithMessage("ChildId must be a positive integer.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than zero.");

        RuleFor(x => x.ReasonCode)
            .NotEmpty()
            .WithMessage("ReasonCode is required.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithMessage("IdempotencyKey is required.");
    }
}

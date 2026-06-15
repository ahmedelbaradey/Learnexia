using FluentValidation;
using Learnexia.Modules.Billing.Application.Features.Credits.Commands.GrantCredit;

namespace Learnexia.Modules.Billing.Application.Features.Credits.Validation;

public class GrantCreditValidation : AbstractValidator<GrantCreditCommand>
{
    public GrantCreditValidation()
    {
        RuleFor(x => x.ChildId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.IdempotencyKey).NotEmpty();
        RuleFor(x => x.ExpiresAtUtc).GreaterThan(DateTime.UtcNow);
    }
}

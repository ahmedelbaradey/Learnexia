using FluentValidation;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.ExpireTimedEvent;

public sealed class ExpireTimedEventCommandValidator : AbstractValidator<ExpireTimedEventCommand>
{
    public ExpireTimedEventCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

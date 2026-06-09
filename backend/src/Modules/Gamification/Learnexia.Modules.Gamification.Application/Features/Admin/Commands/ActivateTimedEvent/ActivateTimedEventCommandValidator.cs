using FluentValidation;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.ActivateTimedEvent;

public sealed class ActivateTimedEventCommandValidator : AbstractValidator<ActivateTimedEventCommand>
{
    public ActivateTimedEventCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

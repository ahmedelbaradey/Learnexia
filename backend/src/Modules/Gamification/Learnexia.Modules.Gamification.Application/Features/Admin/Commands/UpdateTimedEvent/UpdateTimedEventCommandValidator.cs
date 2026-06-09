using FluentValidation;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.UpdateTimedEvent;

public sealed class UpdateTimedEventCommandValidator : AbstractValidator<UpdateTimedEventCommand>
{
    public UpdateTimedEventCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(128);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(128);
        RuleFor(x => x.DescriptionEn).MaximumLength(512).When(x => x.DescriptionEn is not null);
        RuleFor(x => x.DescriptionAr).MaximumLength(512).When(x => x.DescriptionAr is not null);
        RuleFor(x => x.Multiplier)
            .GreaterThanOrEqualTo(1.0m)
            .LessThanOrEqualTo(5.0m);
        RuleFor(x => x.StartUtc)
            .LessThan(x => x.EndUtc).WithMessage("StartUtc must be strictly before EndUtc.");
        RuleFor(x => x.Scope).IsInEnum();
    }
}

using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Units.Commands.SetActive;

public class SetUnitActiveCommandValidator : AbstractValidator<SetUnitActiveCommand>
{
    public SetUnitActiveCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.UnitId)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}

using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Units.Commands.Edit;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Units.Validation;

public class EditUnitValidation : AbstractValidator<EditUnitCommand>
{
    public EditUnitValidation(IStringLocalizer<SharedResources> localizer)
    {
        Include(new UnitBaseValidation(localizer));

        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}

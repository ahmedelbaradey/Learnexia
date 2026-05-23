using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Units.Commands.Add;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Units.Validation;

public class AddUnitValidation : AbstractValidator<AddUnitCommand>
{
    public AddUnitValidation(IStringLocalizer<SharedResources> localizer)
    {
        Include(new UnitBaseValidation(localizer));
    }
}

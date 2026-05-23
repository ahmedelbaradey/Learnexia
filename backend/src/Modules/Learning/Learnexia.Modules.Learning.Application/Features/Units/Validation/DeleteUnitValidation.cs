using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Units.Commands.Delete;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Units.Validation;

public class DeleteUnitValidation : AbstractValidator<DeleteUnitCommand>
{
    public DeleteUnitValidation(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}

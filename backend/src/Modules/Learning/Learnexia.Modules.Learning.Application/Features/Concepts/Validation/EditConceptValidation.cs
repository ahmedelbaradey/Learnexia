using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Concepts.Commands.Edit;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Concepts.Validation;

public class EditConceptValidation : AbstractValidator<EditConceptCommand>
{
    public EditConceptValidation(IStringLocalizer<SharedResources> localizer)
    {
        Include(new ConceptBaseValidation(localizer));

        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}

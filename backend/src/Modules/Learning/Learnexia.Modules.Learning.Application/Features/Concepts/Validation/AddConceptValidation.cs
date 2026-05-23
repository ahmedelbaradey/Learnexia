using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Concepts.Commands.Add;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Concepts.Validation;

public class AddConceptValidation : AbstractValidator<AddConceptCommand>
{
    public AddConceptValidation(IStringLocalizer<SharedResources> localizer)
    {
        Include(new ConceptBaseValidation(localizer));
    }
}

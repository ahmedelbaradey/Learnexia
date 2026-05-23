using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Concepts.Commands.Delete;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Concepts.Validation;

public class DeleteConceptValidation : AbstractValidator<DeleteConceptCommand>
{
    public DeleteConceptValidation(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}

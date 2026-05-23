using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Concepts.Dtos;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Concepts.Validation;

public class ConceptBaseValidation : AbstractValidator<ConceptDto>
{
    public ConceptBaseValidation(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(localizer[SharedResourcesKey.EmptyNameValidation]);

        RuleFor(x => x.DifficultyLevel)
            .IsInEnum().WithMessage(localizer[SharedResourcesKey.RequiredField]);

        RuleFor(x => x.SubjectId)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}

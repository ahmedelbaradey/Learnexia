using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Commands.SetActive;

public class SetSubjectActiveCommandValidator : AbstractValidator<SetSubjectActiveCommand>
{
    public SetSubjectActiveCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.SubjectId)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}

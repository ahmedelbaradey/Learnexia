using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;
using Learnexia.Modules.Learning.Domain.Enums;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Validation;

public class SubjectBaseValidation : AbstractValidator<SubjectDto>
{
    // The 4 valid SubjectCode values (fixed product set).
    private static readonly SubjectCode[] ValidCodes =
    {
        SubjectCode.MATH,
        SubjectCode.SCIENCE,
        SubjectCode.ARABIC,
        SubjectCode.ENGLISH,
    };

    public SubjectBaseValidation(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(localizer[SharedResourcesKey.EmptyNameValidation]);

        RuleFor(x => x.GradeId)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);

        // P7-01: Reject unknown / 5th SubjectCode.
        RuleFor(x => x.SubjectCode)
            .Must(code => ValidCodes.Contains(code))
            .WithMessage(localizer[SharedResourcesKey.InvalidSubjectCode]);
    }
}

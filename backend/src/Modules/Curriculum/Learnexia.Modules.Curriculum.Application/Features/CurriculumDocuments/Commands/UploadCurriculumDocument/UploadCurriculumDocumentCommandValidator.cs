using FluentValidation;
using Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Commands.UploadCurriculumDocument;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Commands.UploadCurriculumDocument;

/// <summary>
/// FluentValidation validator for <see cref="UploadCurriculumDocumentCommand"/>.
/// Validates metadata fields only — file-level checks (type, size, magic bytes) are performed
/// in the handler because they require async I/O and byte-level inspection (ValidationBehavior
/// runs synchronously and cannot read file bytes). GradeId / SubjectId / Country are validated here.
/// </summary>
public class UploadCurriculumDocumentCommandValidator : AbstractValidator<UploadCurriculumDocumentCommand>
{
    public UploadCurriculumDocumentCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(c => c.GradeId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.CurriculumGradeIdRequired]);

        RuleFor(c => c.SubjectId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.CurriculumSubjectIdRequired]);

        RuleFor(c => c.Country)
            .MaximumLength(64)
            .WithMessage(localizer[SharedResourcesKey.CurriculumCountryTooLong])
            .When(c => c.Country is not null);
    }
}

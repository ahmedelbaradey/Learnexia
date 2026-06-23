using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Commands.ReparseCurriculumDocument;

/// <summary>
/// Validates <see cref="ReparseCurriculumDocumentCommand"/> (BL-02-BE-4).
/// Only metadata-level validation: DocumentId must be a positive integer.
/// Business-rule checks (document existence, in-flight job) live in the handler.
/// </summary>
public class ReparseCurriculumDocumentCommandValidator
    : AbstractValidator<ReparseCurriculumDocumentCommand>
{
    public ReparseCurriculumDocumentCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(c => c.DocumentId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.CurriculumDocumentIdRequired]);
    }
}

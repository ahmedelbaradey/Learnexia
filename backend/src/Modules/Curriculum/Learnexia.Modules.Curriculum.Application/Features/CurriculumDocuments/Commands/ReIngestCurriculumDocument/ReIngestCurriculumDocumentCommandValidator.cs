using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Commands.ReIngestCurriculumDocument;

/// <summary>
/// Validates <see cref="ReIngestCurriculumDocumentCommand"/> (BL-05-BE-9).
/// Metadata-level only: DocumentId must be a positive integer.
/// Business-rule checks (document existence, artifact key, in-flight job) live in the handler.
/// </summary>
public class ReIngestCurriculumDocumentCommandValidator
    : AbstractValidator<ReIngestCurriculumDocumentCommand>
{
    public ReIngestCurriculumDocumentCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(c => c.DocumentId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.CurriculumDocumentIdRequired]);
    }
}

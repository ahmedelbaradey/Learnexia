using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Commands.BuildKGSuggestions;

/// <summary>
/// Validates <see cref="BuildKnowledgeGraphSuggestionsCommand"/> (BL-03-BE-3).
/// Metadata-level only: SubjectCode must be in [0,3]; GradeId must be positive.
/// Business-rule checks (node existence, in-flight job) live in the handler.
/// </summary>
public class BuildKnowledgeGraphSuggestionsCommandValidator
    : AbstractValidator<BuildKnowledgeGraphSuggestionsCommand>
{
    public BuildKnowledgeGraphSuggestionsCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(c => c.SubjectCode)
            .InclusiveBetween(0, 3)
            .WithMessage(localizer[SharedResourcesKey.CurriculumSubjectIdRequired]);

        RuleFor(c => c.GradeId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.CurriculumGradeIdRequired]);
    }
}

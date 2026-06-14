using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Ai.Application.Features.Explain.Commands;

/// <summary>
/// Validates <see cref="ExplainConceptCommand"/> via <c>ValidationBehavior</c> (runs for
/// <c>ICommand&lt;T&gt;</c> only — not for queries).
///
/// Rules (per P3-04-BE-1):
/// <list type="bullet">
///   <item>At least one of <c>LessonId</c>, <c>ConceptId</c>, <c>SkillId</c> must be non-null
///         and positive — prevents a free-floating global explain that bypasses the scope guard.</item>
///   <item><c>Question</c> (optional free-text) must not exceed 500 characters — cost/injection bound.</item>
/// </list>
/// </summary>
public sealed class ExplainConceptCommandValidator : AbstractValidator<ExplainConceptCommand>
{
    public ExplainConceptCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        // At least one context anchor is required (scope guard — AC-3 / FR-AI-5).
        RuleFor(c => c)
            .Must(c => (c.LessonId.HasValue && c.LessonId > 0)
                    || (c.ConceptId.HasValue && c.ConceptId > 0)
                    || (c.SkillId.HasValue && c.SkillId > 0))
            .WithMessage(localizer[SharedResourcesKey.ExplainConceptContextRequired]);

        // Optional free-text question — 500-char cap (cost / injection bound).
        When(c => c.Question is not null, () =>
        {
            RuleFor(c => c.Question)
                .MaximumLength(500)
                .WithMessage(localizer[SharedResourcesKey.ExplainConceptQuestionTooLong]);
        });
    }
}

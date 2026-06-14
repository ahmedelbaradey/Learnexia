using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Ai.Application.Features.Simplify.Commands;

/// <summary>
/// Validates <see cref="SimplifyExplanationCommand"/> via <c>ValidationBehavior</c>.
///
/// Rules (per P3-05-BE-2):
/// <list type="bullet">
///   <item>At least one of <c>ConceptId</c> or <c>LessonId</c> must be non-null and positive —
///         prevents a free-floating simplify that bypasses the scope guard.</item>
/// </list>
/// </summary>
public sealed class SimplifyExplanationCommandValidator : AbstractValidator<SimplifyExplanationCommand>
{
    public SimplifyExplanationCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(c => c)
            .Must(c => (c.ConceptId.HasValue && c.ConceptId > 0)
                    || (c.LessonId.HasValue && c.LessonId > 0))
            .WithMessage(localizer[SharedResourcesKey.SimplifyContextRequired]);
    }
}

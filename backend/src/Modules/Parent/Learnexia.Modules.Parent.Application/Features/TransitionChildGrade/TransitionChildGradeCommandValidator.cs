using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Parent.Application.Features.TransitionChildGrade;

/// <summary>
/// Shape-only validation for <see cref="TransitionChildGradeCommand"/> (P5-06).
///
/// Rules:
///   - <c>ChildId</c> must be positive.
///   - <c>TargetGrade</c> must be in [1..6] (product rule: 6 school grades — copied from
///     <c>OverrideChildGradeCommandValidator</c> which uses the same constraint).
///
/// NOT validated here:
///   - Family-scope / IDOR — link check is performed in the handler (anti-enumeration).
///   - Same-grade no-op — idempotency check is performed in the Identity seam.
/// </summary>
public class TransitionChildGradeCommandValidator : AbstractValidator<TransitionChildGradeCommand>
{
    public TransitionChildGradeCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.ChildId)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.ProfileRequiredField]);

        RuleFor(x => x.TargetGrade)
            .InclusiveBetween(1, 6).WithMessage(localizer[SharedResourcesKey.ChildGradeOutOfRange]);
    }
}

using FluentValidation;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.OverrideChildGrade;

/// <summary>
/// Validator for <see cref="OverrideChildGradeCommand"/> (P7-08 BE-3).
/// ICommand validators are picked up automatically by ValidationBehavior.
///
/// Validation rules:
///   - ChildId must be positive.
///   - Grade must be in [1..6] (product rule: 6 school grades, no grade-0 or grade-7).
///   - Reason, when supplied, must not exceed 500 chars.
///
/// NOT validated here:
///   - <c>Confirm</c> — the soft UX guard is enforced in the handler (BadRequest, not 422).
///   - Child-role guard and no-op same-grade check — performed in the handler.
/// </summary>
public class OverrideChildGradeCommandValidator : AbstractValidator<OverrideChildGradeCommand>
{
    public OverrideChildGradeCommandValidator()
    {
        RuleFor(x => x.ChildId)
            .GreaterThan(0)
            .WithMessage(SharedResourcesKey.ChildIdRequired);

        RuleFor(x => x.Grade)
            .InclusiveBetween(1, 6)
            .WithMessage(SharedResourcesKey.ChildGradeOutOfRange);

        When(x => x.Reason is not null, () =>
        {
            RuleFor(x => x.Reason!)
                .MaximumLength(500)
                .WithMessage(SharedResourcesKey.ChildGradeOverrideReasonTooLong);
        });
    }
}

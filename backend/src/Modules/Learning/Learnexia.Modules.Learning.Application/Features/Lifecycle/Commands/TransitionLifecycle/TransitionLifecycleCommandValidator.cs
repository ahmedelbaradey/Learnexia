using FluentValidation;
using Learnexia.Modules.Learning.Domain.Enums;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Lifecycle.Commands.TransitionLifecycle;

/// <summary>
/// FluentValidation validator for <see cref="TransitionLifecycleCommand"/>.
/// Validates that EntityId is positive and that EntityType and TargetState are valid enum members.
/// </summary>
public class TransitionLifecycleCommandValidator : AbstractValidator<TransitionLifecycleCommand>
{
    public TransitionLifecycleCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.EntityId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.EntityIdRequired]);

        RuleFor(x => x.EntityType)
            .Must(v => Enum.IsDefined(typeof(VersionedEntityType), v))
            .WithMessage(localizer[SharedResourcesKey.InvalidVersionedEntityType]);

        RuleFor(x => x.TargetState)
            .Must(v => v == LifecycleState.Draft || v == LifecycleState.Published || v == LifecycleState.Archived)
            .WithMessage(localizer[SharedResourcesKey.IllegalLifecycleTransition]);
    }
}

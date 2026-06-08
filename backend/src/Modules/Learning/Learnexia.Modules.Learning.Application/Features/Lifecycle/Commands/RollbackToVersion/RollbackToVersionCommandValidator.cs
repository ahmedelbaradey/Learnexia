using FluentValidation;
using Learnexia.Modules.Learning.Domain.Enums;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Lifecycle.Commands.RollbackToVersion;

/// <summary>
/// FluentValidation validator for <see cref="RollbackToVersionCommand"/>.
/// </summary>
public class RollbackToVersionCommandValidator : AbstractValidator<RollbackToVersionCommand>
{
    public RollbackToVersionCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.EntityId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.EntityIdRequired]);

        RuleFor(x => x.EntityType)
            .Must(v => Enum.IsDefined(typeof(VersionedEntityType), v))
            .WithMessage(localizer[SharedResourcesKey.InvalidVersionedEntityType]);

        RuleFor(x => x.VersionNumber)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.VersionNumberMustBePositive]);
    }
}

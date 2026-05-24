using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Parent.Application.Features.UnlinkChild;

// Shape-only. The family-scope link check and last-parent guard are enforced in the handler.
public class UnlinkChildCommandValidator : AbstractValidator<UnlinkChildCommand>
{
    private readonly IStringLocalizer<SharedResources> _localizer;

    public UnlinkChildCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        _localizer = localizer;

        RuleFor(x => x.ChildId)
            .GreaterThan(0).WithMessage(_localizer[SharedResourcesKey.ProfileRequiredField]);
    }
}

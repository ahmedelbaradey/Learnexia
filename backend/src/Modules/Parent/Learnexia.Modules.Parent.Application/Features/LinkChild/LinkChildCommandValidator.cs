using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Parent.Application.Features.LinkChild;

// Shape-only: presence + email format. Existence / role / cross-family checks are in the handler and
// collapse to a single generic "cannot link" message (anti-enumeration) — the validator must NOT do an
// existence lookup here.
public class LinkChildCommandValidator : AbstractValidator<LinkChildCommand>
{
    private readonly IStringLocalizer<SharedResources> _localizer;

    public LinkChildCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        _localizer = localizer;

        RuleFor(x => x.ChildEmail)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.ProfileRequiredField])
            .EmailAddress().WithMessage(_localizer[SharedResourcesKey.ProfileInvalidEmailFormat]);
    }
}

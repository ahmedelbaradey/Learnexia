using FluentValidation;
using Learnexia.Modules.Identity.Application.Features.Family.Commands.LinkChild;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Family.Validation;

// Intentionally shape-only: presence + email format. Existence / role / cross-family checks are
// performed in the handler and collapse to a single generic "cannot link" message, so the
// validator must NOT do an existence lookup here — differing 422-vs-200 responses for
// existing-vs-nonexistent emails would be an email-enumeration oracle (AC-5 / security).
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

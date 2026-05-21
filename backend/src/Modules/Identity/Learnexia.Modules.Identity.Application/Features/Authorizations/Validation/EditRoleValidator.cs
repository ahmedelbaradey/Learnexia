using FluentValidation;
using Learnexia.Modules.Identity.Application.Features.Authorizations.Commands.EditRole;

namespace Learnexia.Modules.Identity.Application.Features.Authorizations.Validation;

public class EditRoleValidator : AbstractValidator<EditRoleCommand>
{
    public EditRoleValidator()
    {
        RuleFor(x => x.Id)
            .NotNull().WithMessage("Can't be balnk.");

        RuleFor(x => x.roleName)
            .NotNull().WithMessage("Can't be balnk.");
    }
}

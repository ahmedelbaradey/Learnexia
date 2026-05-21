using FluentValidation;
using Learnexia.Modules.Identity.Application.Features.Authorizations.Commands.AddRole;

namespace Learnexia.Modules.Identity.Application.Features.Authorizations.Validation;

public class AddRoleValidator : AbstractValidator<AddRoleCommand>
{
    public AddRoleValidator()
    {
        RuleFor(x => x.roleName)
            .NotNull().WithMessage("Can't be balnk.");
    }
}

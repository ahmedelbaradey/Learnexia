using FluentValidation;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.EditUser;
using Learnexia.Modules.Identity.Domain.Constants;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Validation;

public class EditUserValidator : AbstractValidator<EditUserCommand>
{
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IIdentityServiceManager _identityServiceManager;

    public EditUserValidator(IStringLocalizer<SharedResources> localizer, IIdentityServiceManager identityServiceManager)
    {
        _localizer = localizer;
        _identityServiceManager = identityServiceManager;
        Include(new BaseUserValidator(_localizer));
        ApplyValidationRules();
    }

    private void ApplyValidationRules()
    {
    }

    private bool BeValidRoleSelection(List<string> roles)
    {
        if (roles == null || roles.Count == 0)
            return false;

        if (roles.Count == 1)
            return true;

        if (roles.Count == 2)
        {
            return (roles.Contains(RoleHelper.FundManager) && roles.Contains(RoleHelper.BoardMember)) ||
                   (roles.Contains(RoleHelper.AssociateFundManager) && roles.Contains(RoleHelper.BoardMember));
        }

        return false;
    }
}

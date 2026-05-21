using System.Text.RegularExpressions;
using FluentValidation;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.AddUser;
using Learnexia.Modules.Identity.Domain.Constants;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Validation;

public class AddUserValidator : AbstractValidator<AddUserCommand>
{
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AddUserValidator(IStringLocalizer<SharedResources> localizer)
    {
        _localizer = localizer;
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

    private bool BeValidSaudiMobileNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return false;

        var saudiMobilePattern = @"^(05|5)(5|0|3|6|4|9|1|8|7)([0-9]{7})$";
        return Regex.IsMatch(phoneNumber, saudiMobilePattern);
    }
}

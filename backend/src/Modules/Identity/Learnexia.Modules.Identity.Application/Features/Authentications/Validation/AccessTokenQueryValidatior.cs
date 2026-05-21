using FluentValidation;
using Learnexia.Modules.Identity.Application.Features.Authentications.Queries.ValidateAccessToken;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Validation;

public class AccessTokenQueryValidatior : AbstractValidator<AccessTokenQuery>
{
    public AccessTokenQueryValidatior()
    {
        RuleFor(x => x.Accesstoken)
            .NotNull().WithMessage("Can't be empty.")
            .NotEmpty().WithMessage("Can't be empty.");
    }
}

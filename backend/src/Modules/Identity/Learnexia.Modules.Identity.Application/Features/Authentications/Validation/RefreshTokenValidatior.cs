using FluentValidation;
using Learnexia.Modules.Identity.Application.Features.Authentications.Commands.RefreshToken;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Validation;

public class RefreshTokenValidatior : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidatior()
    {
        RuleFor(x => x.AccessToken)
            .NotNull().WithMessage("Can't be empty.")
            .NotEmpty().WithMessage("Can't be empty.");

        RuleFor(x => x.RefreshToken)
            .NotNull().WithMessage("Can't be empty.")
            .NotEmpty().WithMessage("Can't be empty.");
    }
}

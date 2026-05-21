using FluentValidation;
using Learnexia.Modules.Catalog.Application.Features.Products.Dtos;

namespace Learnexia.Modules.Catalog.Application.Features.Products.Validation;

public class BaseValidation : AbstractValidator<ProductDto>
{
    public BaseValidation()
    {
        ApplyValidationsRules();
    }

    public void ApplyValidationsRules()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Can't be empty.")
            .NotNull().WithMessage("Can't be blank.");

        RuleFor(x => x.Price)
            .NotEmpty().WithMessage("Can't be empty.")
            .NotNull().WithMessage("Can't be blank.");
    }
}

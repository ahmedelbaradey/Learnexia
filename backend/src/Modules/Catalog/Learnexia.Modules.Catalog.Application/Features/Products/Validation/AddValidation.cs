using FluentValidation;
using Learnexia.Modules.Catalog.Application.Features.Products.Commands.Add;

namespace Learnexia.Modules.Catalog.Application.Features.Products.Validation;

public class AddValidation : AbstractValidator<AddProductCommand>
{
    public AddValidation()
    {
        Include(new BaseValidation());
        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Category Id can't be empty.")
            .NotNull().WithMessage("Category Id can't be blank.");
    }
}

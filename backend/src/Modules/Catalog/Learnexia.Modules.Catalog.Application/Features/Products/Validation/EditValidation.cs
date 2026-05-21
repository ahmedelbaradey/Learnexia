using FluentValidation;
using Learnexia.Modules.Catalog.Application.Features.Products.Commands.Edit;

namespace Learnexia.Modules.Catalog.Application.Features.Products.Validation;

// NOTE: backend/ injected IOptionsMonitor<AttachmentConfiguration> here but never used it. Dropped.
public class EditValidation : AbstractValidator<EditProductCommand>
{
    public EditValidation()
    {
        Include(new BaseValidation());
        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Category Id can't be empty.")
            .NotNull().WithMessage("Category Id can't be blank.");
    }
}

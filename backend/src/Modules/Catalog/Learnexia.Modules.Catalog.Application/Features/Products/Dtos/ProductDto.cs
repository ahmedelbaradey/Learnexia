using Learnexia.Shared.Kernel.Dtos;

namespace Learnexia.Modules.Catalog.Application.Features.Products.Dtos;

public record ProductDto : BaseDto
{
    public string Name { get; set; } = null!;
    public decimal Price { get; set; }
}

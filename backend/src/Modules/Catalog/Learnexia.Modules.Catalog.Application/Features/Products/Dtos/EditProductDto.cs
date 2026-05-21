namespace Learnexia.Modules.Catalog.Application.Features.Products.Dtos;

public record EditProductDto : ProductDto
{
    public string Description { get; set; } = null!;
    public int CategoryId { get; set; }
}

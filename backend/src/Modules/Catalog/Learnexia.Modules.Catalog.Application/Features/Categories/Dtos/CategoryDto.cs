using Learnexia.Shared.Kernel.Dtos;

namespace Learnexia.Modules.Catalog.Application.Features.Categories.Dtos;

public record CategoryDto : BaseDto
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
}

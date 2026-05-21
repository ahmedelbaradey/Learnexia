using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Catalog.Domain.Entities;

public class Product : FullAuditedEntity
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
}

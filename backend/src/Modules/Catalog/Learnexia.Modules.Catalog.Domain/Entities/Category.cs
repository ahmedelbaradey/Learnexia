using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Catalog.Domain.Entities;

public class Category : FullAuditedEntity
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public List<Product> Products { get; set; } = null!;
}

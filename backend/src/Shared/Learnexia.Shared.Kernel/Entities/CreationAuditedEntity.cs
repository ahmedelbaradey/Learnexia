namespace Learnexia.Shared.Kernel.Entities;

// NOTE: backend/ had a CreatedByUser navigation to the Identity User entity. Dropped here to keep
// module isolation — the CreatedBy int FK column remains; cross-module navigation is not modeled.
public class CreationAuditedEntity : BaseEntity
{
    public DateTime CreatedAt { get; set; }
    public int CreatedBy { get; set; }
}

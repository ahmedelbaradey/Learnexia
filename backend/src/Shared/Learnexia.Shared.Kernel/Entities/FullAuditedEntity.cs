namespace Learnexia.Shared.Kernel.Entities;

public class FullAuditedEntity : AduitedEntity
{
    public DateTime? DeletedAt { get; set; }
    public bool? IsDeleted { get; set; }
    public int? DeletedBy { get; set; }
}

namespace Learnexia.Shared.Kernel.Entities;

public class AduitedEntity : CreationAuditedEntity
{
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }
}

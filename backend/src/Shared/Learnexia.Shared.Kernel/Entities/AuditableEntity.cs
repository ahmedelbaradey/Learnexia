namespace Learnexia.Shared.Kernel.Entities;

public abstract class AuditableEntity<TId> : Entity<TId> where TId : notnull
{
    protected AuditableEntity(TId id) : base(id) { }
    protected AuditableEntity() { }

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}

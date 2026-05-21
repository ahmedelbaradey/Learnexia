namespace Learnexia.Modules.Identity.Domain.Entities;

public class UserAuditHistory
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Action { get; set; } = null!;
    public string? Details { get; set; }
    public DateTime AtUtc { get; set; } = DateTime.Now;
}

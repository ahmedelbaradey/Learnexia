using System.ComponentModel.DataAnnotations;

namespace Learnexia.Modules.Identity.Domain.Entities;

public class UserSession
{
    [Key]
    public string SessionId { get; set; } = null!;
    public int UserId { get; set; }
    public string? JwtTokenId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string? TerminationReason { get; set; }

    public bool IsExpired => DateTime.Now > ExpiresAt;
    public TimeSpan RemainingTime => IsExpired ? TimeSpan.Zero : ExpiresAt - DateTime.Now;

    public void ExtendSession(int timeoutMinutes)
    {
        LastActivityAt = DateTime.Now;
        ExpiresAt = DateTime.Now.AddMinutes(timeoutMinutes);
    }

    public void Terminate(string reason)
    {
        IsActive = false;
        TerminationReason = reason;
    }

    public void UpdateActivity()
    {
        LastActivityAt = DateTime.Now;
    }
}

namespace Learnexia.Modules.Identity.Domain.Helpers;

public record SessionInfo
{
    public string SessionId { get; set; } = null!;
    public bool IsActive { get; set; }
    public bool IsExpired { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int RemainingSeconds { get; set; }
    public DateTime LastActivityAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public record SessionValidationResponse
{
    public bool IsValid { get; set; }
    public string? FailureReason { get; set; }
    public SessionInfo? SessionInfo { get; set; }
    public bool WasExtended { get; set; }
}

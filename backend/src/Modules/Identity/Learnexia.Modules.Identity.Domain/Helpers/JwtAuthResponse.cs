namespace Learnexia.Modules.Identity.Domain.Helpers;

public record JwtAuthResponse
{
    public string? AccessToken { get; set; }
    public RefreshToken refreshToken { get; set; } = null!;
    public int UserId { get; set; }
    public bool? IsFirstLogin { get; set; }
    public SessionTimeoutInfo? SessionTimeout { get; set; }
    public string? SessionId { get; set; }
}

public record RefreshToken
{
    public string UserName { get; set; } = null!;
    public DateTime ExpireAt { get; set; }
    public string TokenString { get; set; } = null!;
}

public record SessionTimeoutInfo
{
    public int TimeoutMinutes { get; set; }
    public bool SlidingExpiration { get; set; }
    public DateTime ExpiresAt { get; set; }
}

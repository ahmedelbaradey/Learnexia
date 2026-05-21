namespace Learnexia.Modules.Identity.Domain.Helpers;

public record SessionSettings
{
    public int TimeoutMinutes { get; set; } = 30;
    public bool EnableSlidingExpiration { get; set; } = true;
    public TimeSpan SessionTimeout => TimeSpan.FromMinutes(TimeoutMinutes);
}

namespace Learnexia.Modules.Identity.Domain.Helpers;

public class RoleClaim
{
    public string Type { get; set; } = null!;
    public string Value { get; set; } = null!;
}

public class RoleClaims : RoleClaim
{
    public bool HasClaim { get; set; }
}

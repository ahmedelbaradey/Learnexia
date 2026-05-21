namespace Learnexia.Modules.Identity.Domain.Helpers;

public class Permission
{
    public string RoleId { get; set; } = null!;
    public IList<RoleClaim> RoleClaims { get; set; } = null!;
}

namespace Learnexia.Modules.Identity.Domain.Helpers;

public record ManageUserRolesResponse
{
    public int UserId { get; set; }
    public List<UserRoles> UserRoles { get; set; } = null!;
}

public record UserRoles
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public bool HasRole { get; set; }
}

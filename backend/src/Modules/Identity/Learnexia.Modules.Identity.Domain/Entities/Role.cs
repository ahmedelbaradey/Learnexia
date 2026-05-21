using Microsoft.AspNetCore.Identity;

namespace Learnexia.Modules.Identity.Domain.Entities;

public class Role : IdentityRole<int>
{
    public virtual ICollection<User> Users { get; set; } = new List<User>();
}

using System.Security.Claims;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Modules.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Learnexia.Modules.Identity.Infrastructure.Persistence.Seed;

public static class UserSeeder
{
    private const string DefaultPassword = "123Pa$$word!";

    public static async Task SeedBasicUserAsync(UserManager<User> userManager, RoleManager<Role> roleManager)
    {
        var defaultUser = new User
        {
            UserName = "basicuser",
            Email = "basicuser@gmail.com",
            EmailConfirmed = true,
            PhoneNumberConfirmed = true,
            FullName = "Basic User",
        };
        if (userManager.Users.All(u => u.Id != defaultUser.Id))
        {
            var user = await userManager.FindByEmailAsync(defaultUser.Email);
            if (user == null)
            {
                await userManager.CreateAsync(defaultUser, DefaultPassword);
                await userManager.AddToRoleAsync(defaultUser, Roles.Basic.ToString());
            }
        }
    }

    public static async Task SeedSuperAdminAsync(UserManager<User> userManager, RoleManager<Role> roleManager)
    {
        var defaultUser = new User
        {
            UserName = "superadmin",
            Email = "superadmin@gmail.com",
            EmailConfirmed = true,
            PhoneNumberConfirmed = true,
            FullName = "System Admin",
        };
        if (userManager.Users.All(u => u.Id != defaultUser.Id))
        {
            var user = await userManager.FindByEmailAsync(defaultUser.Email);
            if (user == null)
            {
                await userManager.CreateAsync(defaultUser, DefaultPassword);
                await userManager.AddToRoleAsync(defaultUser, Roles.Basic.ToString());
                await userManager.AddToRoleAsync(defaultUser, Roles.Admin.ToString());
                await userManager.AddToRoleAsync(defaultUser, Roles.SuperAdmin.ToString());
            }
            await roleManager.SeedClaimsForSuperAdmin();
        }
    }

    private static async Task SeedClaimsForSuperAdmin(this RoleManager<Role> roleManager)
    {
        var role = await roleManager.FindByNameAsync("SuperAdmin");
        var claims = await roleManager.GetClaimsAsync(role!);
        foreach (var permission in Claims.GeneratePermissions())
        {
            if (!claims.Any(c => c.Type == CustomClaimTypes.Permission && c.Value == permission))
                await roleManager.AddClaimAsync(role!, new Claim(CustomClaimTypes.Permission, permission));
        }
    }

   
}

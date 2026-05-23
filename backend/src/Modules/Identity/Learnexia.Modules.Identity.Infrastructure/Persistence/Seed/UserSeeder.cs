using System.Security.Claims;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Modules.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace Learnexia.Modules.Identity.Infrastructure.Persistence.Seed;

public static class UserSeeder
{
    // Committed convenience password for the legacy dev-only accounts. Never used in non-Development:
    // IdentitySeeder only invokes SeedBasicUserAsync/SeedSuperAdminAsync when IsDevelopment() is true.
    private const string DefaultPassword = "123Pa$$word!";

    // Configuration keys for the env-driven product Admin seed (BE-3). The password is supplied
    // out-of-band (env var AdminSeed__Password / secret store) — never committed to appsettings.
    private const string AdminSeedSection = "AdminSeed";
    private const string AdminSeedEmailKey = "AdminSeed:Email";
    private const string AdminSeedPasswordKey = "AdminSeed:Password";

    /// <summary>
    /// Idempotently provisions the product Admin account from configuration/environment, with no
    /// committed credential. Reads <c>AdminSeed:Email</c> + <c>AdminSeed:Password</c> (password from
    /// env/secret store). Ensures the Admin role exists and creates the admin only if it is missing.
    /// When the config section is not provided, the seed is a no-op (so dev keeps working via the
    /// legacy accounts, and a deployment that hasn't provisioned config simply skips it).
    /// </summary>
    public static async Task SeedConfiguredAdminAsync(
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        IConfiguration configuration)
    {
        var email = configuration[AdminSeedEmailKey];
        var password = configuration[AdminSeedPasswordKey];

        // No config provided → nothing to seed (no committed fallback credential).
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        // Ensure the Admin role exists (RoleSeeder already seeds it; this is a defensive backstop
        // so the admin seed is self-contained and ordering-independent).
        var adminRoleName = Roles.Admin.ToString();
        if (await roleManager.FindByNameAsync(adminRoleName) is null)
            await roleManager.CreateAsync(new Role { Name = adminRoleName });

        // Idempotent: only create when the account does not already exist.
        var existing = await userManager.FindByEmailAsync(email);
        if (existing != null)
            return;

        var admin = new User
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            PhoneNumberConfirmed = true,
            FullName = "Administrator",
            RegistrationIsCompleted = true,
        };

        var result = await userManager.CreateAsync(admin, password);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, adminRoleName);
    }

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

    /// <summary>
    /// Ensures the SuperAdmin role holds all permission claims, independent of the legacy dev user.
    /// Used in non-Development where SeedSuperAdminAsync is not run.
    /// </summary>
    public static Task SeedSuperAdminClaimsAsync(RoleManager<Role> roleManager)
        => roleManager.SeedClaimsForSuperAdmin();

    private static async Task SeedClaimsForSuperAdmin(this RoleManager<Role> roleManager)
    {
        var role = await roleManager.FindByNameAsync(Roles.SuperAdmin.ToString());
        if (role is null)
            return;

        var claims = await roleManager.GetClaimsAsync(role);
        foreach (var permission in Claims.GeneratePermissions())
        {
            if (!claims.Any(c => c.Type == CustomClaimTypes.Permission && c.Value == permission))
                await roleManager.AddClaimAsync(role, new Claim(CustomClaimTypes.Permission, permission));
        }
    }
}

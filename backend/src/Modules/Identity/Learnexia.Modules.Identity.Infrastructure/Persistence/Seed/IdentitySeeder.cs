using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Learnexia.Modules.Identity.Infrastructure.Persistence.Seed;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        // Apply any pending Identity migrations before seeding.
        var dbContext = serviceProvider.GetRequiredService<IdentityModuleDbContext>();
        await dbContext.Database.MigrateAsync();

        var userManager = serviceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<Role>>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var environment = serviceProvider.GetRequiredService<IHostEnvironment>();

        await RoleSeeder.SeedAsync(roleManager);

        // Config/env-driven product Admin seed (no committed credential) — runs in every environment.
        await UserSeeder.SeedConfiguredAdminAsync(userManager, roleManager, configuration);

        // Legacy convenience accounts ship a committed default password, so they are seeded only in
        // Development. In non-Development we never create accounts with a hardcoded credential.
        if (environment.IsDevelopment())
        {
            await UserSeeder.SeedBasicUserAsync(userManager, roleManager);
            await UserSeeder.SeedSuperAdminAsync(userManager, roleManager);
        }
        else
        {
            // SuperAdmin role claims are normally seeded as a side-effect of SeedSuperAdminAsync (dev only).
            // Ensure permission claims exist in non-Development too, independent of the legacy user.
            await UserSeeder.SeedSuperAdminClaimsAsync(roleManager);
        }
    }
}

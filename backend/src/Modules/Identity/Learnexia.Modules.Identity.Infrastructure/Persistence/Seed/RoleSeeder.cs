using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Modules.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Learnexia.Modules.Identity.Infrastructure.Persistence.Seed;

public static class RoleSeeder
{
    // Seed every role idempotently. Names are standardized to the enum's PascalCase
    // (e.g. "Parent", "SuperAdmin") — UserSeeder and SeedClaimsForSuperAdmin already use
    // PascalCase, so this aligns the whole module. ASP.NET Identity stores a NormalizedName
    // (upper-case) used for all lookups, so on a DB previously seeded with lower-case names
    // the existence check still matches (NormalizedName is identical) and no duplicate row is
    // created. The role Name is emitted verbatim as the JWT role claim and matched ordinally by
    // the AdminOnly policy, so when an existing row's Name differs only by casing we self-heal it
    // to the PascalCase value (RoleManager.UpdateAsync recomputes NormalizedName). RoleHelper's
    // lower-case constants drive only legacy role-check helpers that are not used by the
    // Register-Parent flow; leaving them is a separate cleanup story.
    public static async Task SeedAsync(RoleManager<Role> roleManager)
    {
        await EnsureRoleAsync(roleManager, Roles.SuperAdmin);
        await EnsureRoleAsync(roleManager, Roles.Admin);
        await EnsureRoleAsync(roleManager, Roles.Basic);
        await EnsureRoleAsync(roleManager, Roles.FundManager);
        await EnsureRoleAsync(roleManager, Roles.LegalCouncil);
        await EnsureRoleAsync(roleManager, Roles.BoardSecretary);
        await EnsureRoleAsync(roleManager, Roles.BoardMember);
        await EnsureRoleAsync(roleManager, Roles.FinanceController);
        await EnsureRoleAsync(roleManager, Roles.ComplianceLegalManagingDirector);
        await EnsureRoleAsync(roleManager, Roles.HeadOfRealEstate);
        await EnsureRoleAsync(roleManager, Roles.AssociatedFundManager);
        await EnsureRoleAsync(roleManager, Roles.Parent);
        await EnsureRoleAsync(roleManager, Roles.Student);
    }

    private static async Task EnsureRoleAsync(RoleManager<Role> roleManager, Roles role)
    {
        var name = role.ToString();

        // Lookup is by NormalizedName, so a previously lower-case-seeded role ("admin") is found
        // by its PascalCase enum value ("Admin") since both normalize identically.
        var existing = await roleManager.FindByNameAsync(name);
        if (existing is null)
        {
            await roleManager.CreateAsync(new Role { Name = name });
            return;
        }

        // Self-heal casing: converge a legacy lower-case Name to the canonical PascalCase value.
        // Use ordinal comparison so only a true casing/value drift triggers an update — keeping it
        // idempotent (no-op when already PascalCase). UpdateAsync recomputes NormalizedName.
        if (!string.Equals(existing.Name, name, StringComparison.Ordinal))
        {
            existing.Name = name;
            await roleManager.UpdateAsync(existing);
        }
    }
}

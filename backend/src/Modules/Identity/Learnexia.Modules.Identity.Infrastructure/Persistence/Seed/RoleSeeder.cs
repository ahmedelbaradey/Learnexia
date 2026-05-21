using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Modules.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Learnexia.Modules.Identity.Infrastructure.Persistence.Seed;

public static class RoleSeeder
{
    public static async Task SeedAsync(RoleManager<Role> roleManager)
    {
        await roleManager.CreateAsync(new Role { Name = Roles.SuperAdmin.ToString().ToLower() });
        await roleManager.CreateAsync(new Role { Name = Roles.Admin.ToString().ToLower() });
        await roleManager.CreateAsync(new Role { Name = Roles.Basic.ToString().ToLower() });
        await roleManager.CreateAsync(new Role { Name = Roles.FundManager.ToString().ToLower() });
        await roleManager.CreateAsync(new Role { Name = Roles.LegalCouncil.ToString().ToLower() });
        await roleManager.CreateAsync(new Role { Name = Roles.BoardSecretary.ToString().ToLower() });
        await roleManager.CreateAsync(new Role { Name = Roles.BoardMember.ToString().ToLower() });
        await roleManager.CreateAsync(new Role { Name = Roles.FinanceController.ToString().ToLower() });
        await roleManager.CreateAsync(new Role { Name = Roles.ComplianceLegalManagingDirector.ToString().ToLower() });
        await roleManager.CreateAsync(new Role { Name = Roles.HeadOfRealEstate.ToString().ToLower() });
        await roleManager.CreateAsync(new Role { Name = Roles.AssociatedFundManager.ToString().ToLower() });
    }
}

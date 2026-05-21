using System.Security.Claims;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Identity.Infrastructure.Services;

public class AuthorizationIdentityService : IAuthorizationService
{
    private readonly RoleManager<Role> _roleManager;
    private readonly UserManager<User> _userManager;
    private readonly ILoggerManager _logger;

    public AuthorizationIdentityService(RoleManager<Role> roleManager, UserManager<User> userManager, ILoggerManager logger)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<bool> AddRoleAsync(string roleName, List<RoleClaims> roleClaims)
    {
        try
        {
            var role = new Role { Name = roleName.Trim().ToLower() };

            var result = await _roleManager.CreateAsync(role);
            var addedClaims = roleClaims.Where(c => c.HasClaim).Select(a => a.Value).ToList();
            var affectedClaims = 0;
            if (addedClaims.Any())
            {
                foreach (var claim in addedClaims)
                {
                    affectedClaims++;
                    await _roleManager.AddClaimAsync(role, new Claim(CustomClaimTypes.Permission, claim));
                }
            }

            if (!result.Succeeded)
                return false;

            return result.Succeeded || affectedClaims > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AddRoleAsync");
            throw;
        }
    }

    public async Task<bool> EditRoleById(int Id, string roleName, List<RoleClaims> roleClaims)
    {
        try
        {
            var role = await _roleManager.FindByIdAsync(Id.ToString());
            if (role == null)
                return false;

            role.Name = roleName;
            var result = await _roleManager.UpdateAsync(role);
            if (!result.Succeeded)
                return false;

            var dbClaims = await _roleManager.GetClaimsAsync(role);
            var dbClaimNames = dbClaims.Select(a => a.Value).ToList();
            var addedClaims = roleClaims.Where(c => c.HasClaim).Select(a => a.Value).ToList();
            var deletedClaims = roleClaims.Where(c => !c.HasClaim).Select(a => a.Value).ToList();
            var affectedClaims = 0;

            if (addedClaims.Any())
            {
                foreach (var claim in addedClaims)
                {
                    if (!dbClaimNames.Contains(claim))
                    {
                        affectedClaims++;
                        await _roleManager.AddClaimAsync(role, new Claim(CustomClaimTypes.Permission, claim));
                    }
                }
            }

            if (dbClaims.Any())
            {
                foreach (var claim in dbClaims)
                {
                    if (deletedClaims.Contains(claim.Value))
                    {
                        affectedClaims++;
                        await _roleManager.RemoveClaimAsync(role, claim);
                    }
                }
            }

            return result.Succeeded || affectedClaims > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in EditRoleById");
            throw;
        }
    }

    public async Task<bool> DeleteRoleById(Role role)
    {
        try
        {
            var users = await _userManager.GetUsersInRoleAsync(role.Name!);
            if (users != null && users.Count() > 0)
                return false;

            var result = await _roleManager.DeleteAsync(role);
            return result.Succeeded;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteRoleById");
            throw;
        }
    }

    public async Task<bool> IsRoleNameExist(string rolename)
    {
        try
        {
            return await _roleManager.RoleExistsAsync(rolename.ToLower());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in IsRoleNameExist");
            throw;
        }
    }

    public async Task<Role> GetRoleByID(int Id)
    {
        try
        {
            return (await _roleManager.FindByIdAsync(Id.ToString()))!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetRoleByID");
            throw;
        }
    }

    public async Task<List<Role>> GetRoleListAsync()
    {
        try
        {
            return _roleManager.Roles.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetRoleListAsync");
            throw;
        }
    }

    public async Task<User> GetUserByRoles(int userId)
    {
        try
        {
            return (await _userManager.Users.Include(u => u.Roles).FirstOrDefaultAsync(c => c.Id == userId))!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetUserByRoles");
            throw;
        }
    }

    public async Task<ManageUserRolesResponse> GetUsersRoles(User user)
    {
        try
        {
            var userRoles = new List<UserRoles>();
            var response = new ManageUserRolesResponse();
            var rolesForUser = await _userManager.GetRolesAsync(user);
            var rolesInSystem = _roleManager.Roles.ToList();
            foreach (var role in rolesInSystem)
            {
                userRoles.Add(new UserRoles
                {
                    Id = role.Id,
                    Name = role.Name!,
                    HasRole = rolesForUser.Contains(role.Name!),
                });
            }

            response.UserId = user.Id;
            response.UserRoles = userRoles;
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetUsersRoles");
            throw;
        }
    }

    public async Task<string> UpdateUserRoles(EditUserRolesRequest request)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(request.UserId.ToString());
            if (user == null)
                return "UserNotFound";

            var rolesForUser = await _userManager.GetRolesAsync(user);
            var addedRoles = request.UserRoles.Where(x => x.HasRole).Select(x => x.Name);
            foreach (var item in addedRoles)
            {
                if (!rolesForUser.Contains(item))
                    await _userManager.AddToRoleAsync(user, item);
            }

            var deleted = request.UserRoles.Where(x => !x.HasRole).Select(x => x.Name);
            foreach (var item in deleted)
            {
                if (rolesForUser.Contains(item))
                    await _userManager.RemoveFromRoleAsync(user, item);
            }

            return "Success";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UpdateUserRoles");
            return "FaildAdded";
        }
    }

    public async Task<string> EditUserRoles(List<string> roles, User user)
    {
        try
        {
            var rolesForUser = await _userManager.GetRolesAsync(user);
            foreach (var item in roles)
            {
                if (!rolesForUser.Contains(item))
                    await _userManager.AddToRoleAsync(user, item);
            }

            var deleted = rolesForUser.Except(roles).ToList();
            foreach (var item in deleted)
            {
                if (rolesForUser.Contains(item))
                    await _userManager.RemoveFromRoleAsync(user, item);
            }

            return "Success";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in EditUserRoles");
            return "FaildAdded";
        }
    }

    public async Task<List<RoleClaims>> GetRoleClaims(string roleId)
    {
        var role = await _roleManager.FindByIdAsync(roleId);
        var claimsInSystem = new List<string>();
        foreach (var module in Claims.GenerateModules())
        {
            foreach (var claim in Claims.GeneratePermissions())
                claimsInSystem.Add($"{claim}");
        }

        var _claims = await _roleManager.GetClaimsAsync(role!);
        var currentRoleClaims = _claims.Select(a => a.Value).ToList();

        var roleClaims = new List<RoleClaims>();
        foreach (var claim in claimsInSystem)
        {
            roleClaims.Add(new RoleClaims
            {
                Value = claim!,
                Type = "Permission",
                HasClaim = currentRoleClaims.Contains(claim!),
            });
        }

        return roleClaims;
    }
}

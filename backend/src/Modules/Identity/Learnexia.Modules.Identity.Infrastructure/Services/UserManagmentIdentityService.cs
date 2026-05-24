using System.Security.Claims;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Identity.Infrastructure.Services;

public class UserManagmentIdentityService : IUserManagmentService
{
    private readonly UserManager<User> _userManager;
    private readonly ILoggerManager _logger;

    public UserManagmentIdentityService(UserManager<User> userManager, ILoggerManager logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IdentityResult> AddToRoleAsync(User user, string role) => await _userManager.AddToRoleAsync(user, role);

    public async Task<IdentityResult> ChangePasswordAsync(User user, string currentPassword, string newPassword)
        => await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

    public async Task<IdentityResult> CreateAsync(User user, string password) => await _userManager.CreateAsync(user, password);

    public async Task<IdentityResult> CreateAsync(User user) => await _userManager.CreateAsync(user);

    public async Task<IList<UserLoginInfo>> GetLoginsAsync(User user) => await _userManager.GetLoginsAsync(user);

    public async Task<IdentityResult> AddLoginAsync(User user, UserLoginInfo login) => await _userManager.AddLoginAsync(user, login);

    public async Task<IdentityResult> DeleteAsync(User user) => await _userManager.DeleteAsync(user);

    public async Task<User?> FindByEmailAsync(string email) => await _userManager.FindByEmailAsync(email);

    public async Task<User?> FindByIdAsync(string id) => await _userManager.FindByIdAsync(id);

    public async Task<User?> FindByNameAsync(string userName) => await _userManager.FindByNameAsync(userName);

    public async Task<IdentityResult> UpdateAsync(User user) => await _userManager.UpdateAsync(user);

    public IQueryable<User> Users() => _userManager.Users;

    public async Task<IEnumerable<User>> GetUsersByRole(string role) => await _userManager.GetUsersInRoleAsync(role.ToLower());

    public async Task<IEnumerable<Claim>> GetClaimsAsync(User user) => await _userManager.GetClaimsAsync(user);

    public async Task UpdateSecurityStampAsync(User user) => await _userManager.UpdateSecurityStampAsync(user);

    public async Task RemoveClaimsAsync(User user, List<Claim> fcmClaims) => await _userManager.RemoveClaimsAsync(user, fcmClaims);

    public async Task<IList<string>> GetUserRolesAsync(User user) => await _userManager.GetRolesAsync(user);

    public async Task<User?> FindByIdWithRolesAsync(string id)
    {
        try
        {
            return await _userManager.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id.ToString() == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving user with roles for ID: {id}");
            return null;
        }
    }

    public IQueryable<User> UsersWithRoles() => _userManager.Users.Include(u => u.Roles);

    public async Task<List<User>> GetUsersWithRolesAsync()
    {
        try
        {
            return await _userManager.Users.Include(u => u.Roles).ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users with roles");
            return new List<User>();
        }
    }

    public async Task<User?> FindActiveUserWithOnlyRoleAsync(string roleName, int? excludeUserId = null)
    {
        try
        {
            var allUsers = await GetUsersWithRolesAsync();
            allUsers = allUsers.FindAll(u => u.Id != excludeUserId!.Value && u.IsActive && u.Roles.Select(c => c.Name).Contains(roleName));
            return allUsers.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error finding active user with only role {roleName}");
            return null;
        }
    }

    public async Task<bool> UserHasMultipleRolesAsync(int userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return false;

            var userRoles = await _userManager.GetRolesAsync(user);
            return userRoles.Count > 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error checking multiple roles for user {userId}");
            return false;
        }
    }

    public async Task<List<User>> GetUsersWithOnlyRoleAsync(string roleName)
    {
        try
        {
            var activeUsers = await _userManager.Users
                .Where(u => u.IsActive && !u.IsDeleted.HasValue || !u.IsDeleted!.Value)
                .ToListAsync();

            var usersWithOnlyRole = new List<User>();
            foreach (var user in activeUsers)
            {
                var userRoles = await _userManager.GetRolesAsync(user);
                if (userRoles.Count == 1 && userRoles.Contains(roleName))
                    usersWithOnlyRole.Add(user);
            }

            return usersWithOnlyRole;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting users with only role {roleName}");
            return new List<User>();
        }
    }
}

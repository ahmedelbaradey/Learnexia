using System.Security.Claims;
using Learnexia.Modules.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Learnexia.Modules.Identity.Application.Abstractions;

public interface IUserManagmentService
{
    Task<User?> FindByEmailAsync(string email);
    Task<User?> FindByNameAsync(string userName);
    Task<IdentityResult> CreateAsync(User user, string password);
    Task<User?> FindByIdAsync(string id);
    Task<IdentityResult> ChangePasswordAsync(User user, string currentPassword, string newPassword);
    Task<IdentityResult> DeleteAsync(User user);
    Task<IdentityResult> UpdateAsync(User user);
    Task<IdentityResult> AddToRoleAsync(User user, string role);
    IQueryable<User> Users();
    Task<IEnumerable<User>> GetUsersByRole(string role);
    Task<IEnumerable<Claim>> GetClaimsAsync(User user);
    Task UpdateSecurityStampAsync(User user);
    Task RemoveClaimsAsync(User user, List<Claim> fcmClaims);
    Task<IList<string>> GetUserRolesAsync(User user);
    Task<User?> FindByIdWithRolesAsync(string id);
    IQueryable<User> UsersWithRoles();
    Task<List<User>> GetUsersWithRolesAsync();
    Task<User?> FindActiveUserWithOnlyRoleAsync(string roleName, int? excludeUserId = null);
    Task<bool> UserHasMultipleRolesAsync(int userId);
    Task<List<User>> GetUsersWithOnlyRoleAsync(string roleName);
}

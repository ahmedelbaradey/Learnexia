using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Domain.Helpers;

namespace Learnexia.Modules.Identity.Application.Abstractions;

public interface IAuthorizationService
{
    Task<bool> AddRoleAsync(string roleName, List<RoleClaims> roleClaims);
    Task<bool> EditRoleById(int Id, string roleName, List<RoleClaims> roleClaims);
    Task<bool> DeleteRoleById(Role role);
    Task<bool> IsRoleNameExist(string rolename);
    Task<Role> GetRoleByID(int Id);
    Task<List<Role>> GetRoleListAsync();
    Task<ManageUserRolesResponse> GetUsersRoles(User user);
    Task<string> UpdateUserRoles(EditUserRolesRequest request);
    Task<List<RoleClaims>> GetRoleClaims(string roleId);
    Task<string> EditUserRoles(List<string> roles, User user);
    Task<User> GetUserByRoles(int userId);
}

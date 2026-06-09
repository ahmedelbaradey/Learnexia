using System.Security.Claims;
using Learnexia.Modules.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Learnexia.Modules.Identity.Application.Abstractions;

public interface IUserManagmentService
{
    Task<User?> FindByEmailAsync(string email);
    Task<User?> FindByNameAsync(string userName);
    Task<IdentityResult> CreateAsync(User user, string password);
    // External-only account creation (no password) — used by social sign-in (P1-12 BE-5).
    Task<IdentityResult> CreateAsync(User user);
    // External-login (e.g. Google) management for social sign-in (P1-12 BE-5).
    Task<IList<UserLoginInfo>> GetLoginsAsync(User user);
    Task<IdentityResult> AddLoginAsync(User user, UserLoginInfo login);
    Task<User?> FindByIdAsync(string id);
    Task<IdentityResult> ChangePasswordAsync(User user, string currentPassword, string newPassword);
    // Self-service password reset (P1-12 BE-6). GeneratePasswordResetTokenAsync mints a single-use,
    // time-limited token; ResetPasswordAsync validates that token and enforces the configured password
    // policy. Both wrap UserManager directly — no token is ever logged.
    Task<string> GeneratePasswordResetTokenAsync(User user);
    Task<IdentityResult> ResetPasswordAsync(User user, string token, string newPassword);
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

    // ── Explicit transaction support (Finding #4 — cascade-delete atomicity) ──
    // Identity module uses UserManager which commits per UpdateAsync call (no UoW).
    // Multi-write operations that must be atomic (e.g. parent + cascade children delete)
    // must open an explicit DB transaction via these methods.
    Task<IIdentityDbTransaction> BeginTransactionAsync(CancellationToken ct = default);
}

/// <summary>
/// Opaque handle to an open <c>IDbContextTransaction</c> for the Identity module DbContext.
/// Returned by <see cref="IUserManagmentService.BeginTransactionAsync"/>.
/// Dispose BEFORE CommitAsync/RollbackAsync; always dispose to free the connection.
/// </summary>
public interface IIdentityDbTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

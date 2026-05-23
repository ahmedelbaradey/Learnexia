using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Shared.Contracts.Identity;
using Microsoft.AspNetCore.Identity;

namespace Learnexia.Modules.Identity.Infrastructure.Services;

// Identity-side implementation of the cross-module IUserLookup seam (Shared.Contracts).
// Lets other modules (Notifications welcome-email, P1-12d password-reset) resolve a user's
// email by id without referencing the Identity module directly. Read-only; mirrors how
// UserManagmentIdentityService queries users via UserManager<User>.FindByIdAsync.
public sealed class UserLookup : IUserLookup
{
    private readonly UserManager<User> _userManager;

    public UserLookup(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task<UserSummary?> FindByIdAsync(int userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return null;

        return new UserSummary(user.Id, user.UserName ?? string.Empty, user.Email ?? string.Empty);
    }
}

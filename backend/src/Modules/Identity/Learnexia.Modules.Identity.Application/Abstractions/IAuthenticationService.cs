using System.IdentityModel.Tokens.Jwt;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Domain.Helpers;
using Microsoft.AspNetCore.Identity;

namespace Learnexia.Modules.Identity.Application.Abstractions;

public interface IAuthenticationService
{
    Task<JwtAuthResponse> GetJwtToken(User user);
    JwtSecurityToken ReadJwtToken(string accessToken);
    Task<(string, DateTime?)> ValidateDetails(JwtSecurityToken jwtToken, string accessToken, string refreshTken);
    Task<JwtAuthResponse> GetRefreshToken(User user, JwtSecurityToken jwtToken, DateTime? expiryDate, string refreshToken);
    Task<string> ValidateJwtToken(string accessToken);
    Task<bool> HasPasswordAsync(User user);
    Task<IdentityResult> RemovePasswordAsync(User user);
    Task<IdentityResult> AddPasswordAsync(User user, string password);
    Task<IdentityResult> ChangePasswordAsync(User user, string currentPassword, string newPassword);
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;

namespace Learnexia.Modules.Identity.Infrastructure.Services;

public class AuthenticationIdentityService : IAuthenticationService
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly JwtSettings _jwtSettings;
    private readonly ILoggerManager _logger;
    private readonly IDistributedCache _distributedCache;

    public AuthenticationIdentityService(UserManager<User> userManager, RoleManager<Role> roleManager, JwtSettings jwtSettings, ILoggerManager logger, IDistributedCache distributedCache)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _jwtSettings = jwtSettings;
        _logger = logger;
        _distributedCache = distributedCache;
    }

    public async Task<JwtAuthResponse> GetJwtToken(User user)
    {
        try
        {
            var (_, accessToken) = await GenerateJwtToken(user);
            return new JwtAuthResponse { AccessToken = accessToken };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetJwtToken");
            throw;
        }
    }

    public async Task<bool> HasPasswordAsync(User user) => await _userManager.HasPasswordAsync(user);

    public async Task<IdentityResult> RemovePasswordAsync(User user) => await _userManager.RemovePasswordAsync(user);

    public async Task<IdentityResult> AddPasswordAsync(User user, string password) => await _userManager.AddPasswordAsync(user, password);

    public async Task<JwtAuthResponse> GetRefreshToken(User user, JwtSecurityToken jwtToken, DateTime? expiryDate, string refreshToken)
    {
        try
        {
            var (_, newToken) = await GenerateJwtToken(user);
            var refreshTokenResult = new RefreshToken
            {
                UserName = jwtToken.Claims.FirstOrDefault(x => x.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")!.Value,
                TokenString = refreshToken,
                ExpireAt = (DateTime)expiryDate!,
            };

            return new JwtAuthResponse
            {
                AccessToken = newToken,
                refreshToken = refreshTokenResult,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetRefreshToken");
            throw;
        }
    }

    public JwtSecurityToken ReadJwtToken(string accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
            throw new ArgumentNullException(nameof(accessToken));

        return new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
    }

    public async Task<(string, DateTime?)> ValidateDetails(JwtSecurityToken jwtToken, string accessToken, string refreshTken)
    {
        try
        {
            if (jwtToken == null || !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256Signature))
                return ("AlgorithmIsWrong", null);

            if (jwtToken.ValidTo > DateTime.Now)
                return ("TokenIsRunning", null);

            var userId = jwtToken.Claims.FirstOrDefault(x => x.Type == "Id")!.Value;
            var userRefreshtoken = await GetById(int.Parse(userId));
            if (userRefreshtoken == null)
                return ("RefreshTokenNotFound", null);

            return (userId, userRefreshtoken.ExpiryDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ValidateDetails");
            throw;
        }
    }

    public async Task<string> ValidateJwtToken(string accessToken)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = _jwtSettings.ValidateIssure,
                ValidIssuers = new[] { _jwtSettings.Issure },
                ValidateIssuerSigningKey = _jwtSettings.ValidateIssureSigningKey,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_jwtSettings.Secret)),
                ValidateAudience = _jwtSettings.validateAudience,
                ValidAudience = _jwtSettings.Audience,
                ValidateLifetime = _jwtSettings.ValidateLifeTime,
            };
            var validator = handler.ValidateToken(accessToken, parameters, out _);
            if (validator == null)
                return "InvalidJwtToken";

            return "NotExpired";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ValidateJwtToken");
            throw;
        }
    }

    public Task<IdentityResult> ChangePasswordAsync(User user, string currentPassword, string newPassword)
        => _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

    private async Task<(JwtSecurityToken, string)> GenerateJwtToken(User user)
    {
        var roleNames = await _userManager.GetRolesAsync(user);
        var claims = await GetClaims(user, roleNames.ToList());

        var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);

        var jwtToken = new JwtSecurityToken(
            _jwtSettings.Issure,
            _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.Now.AddMinutes(_jwtSettings.AccessTokenExpireMinutes),
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwtToken);
        return (jwtToken, accessToken);
    }

    private async Task<List<Claim>> GetClaims(User user, List<string> roles)
    {
        var jwtId = Guid.NewGuid().ToString();
        var sessionId = Guid.NewGuid().ToString();

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.FullName!),
            new(ClaimTypes.NameIdentifier, user.UserName!),
            new(ClaimTypes.Email, user.Email!),
            new(nameof(UserClaimsModel.Id), user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, jwtId),
            new("SessionId", sessionId),
        };

        foreach (var roleName in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, roleName));
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role != null)
            {
                var roleClaims = await _roleManager.GetClaimsAsync(role);
                foreach (var roleClaim in roleClaims)
                    claims.Add(new Claim(CustomClaimTypes.Permission, roleClaim.Value));
            }
        }

        return claims;
    }

    private async Task<UserRefreshToken?> GetById(int userId)
    {
        var key = $"userrefreshtoken-{userId}";
        var json = await _distributedCache.GetStringAsync(key);
        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<UserRefreshToken>(json);
    }

    private static string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}

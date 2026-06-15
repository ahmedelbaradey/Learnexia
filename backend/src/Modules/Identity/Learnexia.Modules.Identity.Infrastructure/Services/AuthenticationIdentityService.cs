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

    // The cache DTO (UserRefreshToken) carries a User navigation property that is always null here;
    // ignore cycles defensively so serialization never throws on the unused navigation.
    private static readonly JsonSerializerOptions RefreshTokenSerializerOptions = new()
    {
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
    };

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
            var (jwtToken, accessToken) = await GenerateJwtToken(user);

            // P1-02: issue + persist a refresh token in Redis on every access-token issuance.
            // This is the SHARED path used by sign-in AND parent registration, so both flows
            // now return a refresh token the client can later exchange at /Refresh-Token.
            var refreshToken = await PersistRefreshToken(user.Id, accessToken, jwtToken);

            return new JwtAuthResponse
            {
                AccessToken = accessToken,
                refreshToken = refreshToken,
            };
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
            var (newJwtToken, newToken) = await GenerateJwtToken(user);

            // P1-02 (AC-8): ROTATE. Generate a brand-new refresh token and overwrite the stored
            // entry under "userrefreshtoken-{userId}". SetStringAsync overwrites in place, so the
            // previously-supplied refresh token is immediately invalid — a replay is rejected by the
            // match check in ValidateDetails.
            var refreshTokenResult = await PersistRefreshToken(user.Id, newToken, newJwtToken);

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

            // JwtSecurityToken.ValidTo is UTC; compare against UtcNow so the "still-valid token"
            // guard fires correctly regardless of the host timezone (was DateTime.Now — a non-UTC
            // server would let a not-yet-expired access token be refreshed).
            if (jwtToken.ValidTo > DateTime.UtcNow)
                return ("TokenIsRunning", null);

            var userId = jwtToken.Claims.FirstOrDefault(x => x.Type == "Id")!.Value;
            var userRefreshtoken = await GetById(int.Parse(userId));
            if (userRefreshtoken == null)
                return ("RefreshTokenNotFound", null);

            // P1-02 (AC-5): the supplied refresh token must match the stored one. Reject an arbitrary
            // string just because a cache entry exists; this also rejects a rotated-away (replayed) token.
            if (!string.Equals(refreshTken, userRefreshtoken.RefreshToken, StringComparison.Ordinal))
                return ("RefreshTokenNotFound", null);

            // P1-02 (AC-4): enforce the 7-day refresh window. Stored ExpiryDate is UTC.
            if (userRefreshtoken.ExpiryDate < DateTime.UtcNow)
                return ("RefreshTokenIsExpired", null);

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
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpireMinutes),
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
            // P8-01: medium-of-instruction language for student curriculum delivery.
            // Absent on non-student accounts (consumers fall back to "ar"). Re-issued on every
            // GenerateJwtToken call, which covers both initial issuance and refresh rotation.
            new(CustomClaimTypes.LearningLanguage, user.LearningLanguage),
        };

        // AI-DEFECT-1: emit Grade + Age claims for student accounts so that AI Helper handlers
        // resolve the real grade from the JWT (not the fallback default of 4).
        // Guard on HasValue — parent/admin accounts have null Grade/Age on the User entity and
        // must not receive these claims. Grade changes (P7-08 OverrideChildGrade) take effect on
        // the next token refresh; live invalidation is not required (documented in HANDOFF.md).
        var isStudent = roles.Any(r => string.Equals(r, Roles.Student.ToString(),
            StringComparison.OrdinalIgnoreCase));
        if (isStudent && user.Grade.HasValue)
            claims.Add(new Claim(CustomClaimTypes.Grade, user.Grade.Value.ToString()));
        if (isStudent && user.Age.HasValue)
            claims.Add(new Claim(CustomClaimTypes.Age, user.Age.Value.ToString()));

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

    // P1-02 (AC-2, AC-6, AC-8): generate a high-entropy refresh token, persist it in Redis under
    // "userrefreshtoken-{userId}" with a 7-day TTL (JwtSettings.RefreshTokenExpireDate), and return the
    // RefreshToken DTO for JwtAuthResponse. Used by both initial issuance (GetJwtToken) and rotation
    // (GetRefreshToken). The write overwrites any existing entry, which is what makes rotation atomic.
    //
    // NOTE (cleanup debt): the persisted model is the EF entity type UserRefreshToken used purely as a
    // cache DTO here; the actual EF table is dormant (Redis is authoritative). See P1-02 plan.
    private async Task<RefreshToken> PersistRefreshToken(int userId, string accessToken, JwtSecurityToken jwtToken)
    {
        var refreshTokenString = GenerateRefreshToken();
        var now = DateTime.UtcNow;
        var expiryDate = now.AddDays(_jwtSettings.RefreshTokenExpireDate);
        var jti = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;

        var stored = new UserRefreshToken
        {
            UserId = userId,
            Token = accessToken,
            RefreshToken = refreshTokenString,
            JwtId = jti,
            AddedTime = now,
            ExpiryDate = expiryDate,
            IsUsed = false,
            IsRevoked = false,
        };

        var options = new DistributedCacheEntryOptions { AbsoluteExpiration = expiryDate };
        await _distributedCache.SetStringAsync(
            $"userrefreshtoken-{userId}",
            JsonSerializer.Serialize(stored, RefreshTokenSerializerOptions),
            options);

        return new RefreshToken
        {
            UserName = jwtToken.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
            TokenString = refreshTokenString,
            ExpireAt = expiryDate,
        };
    }

    private async Task<UserRefreshToken?> GetById(int userId)
    {
        var key = $"userrefreshtoken-{userId}";
        var json = await _distributedCache.GetStringAsync(key);
        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<UserRefreshToken>(json, RefreshTokenSerializerOptions);
    }

    private static string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}

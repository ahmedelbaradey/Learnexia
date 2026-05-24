using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Learnexia.Modules.Parent.Infrastructure.Service;

/// <summary>
/// Reads the current user from the request claims (same claims Identity issues). Mirrors Learning's
/// <c>CurrentUserService</c>.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? UserId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User?.FindFirstValue("Id");
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public string? UserName => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name);

    public IList<string> Roles =>
        _httpContextAccessor.HttpContext?.User?.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList() ?? new List<string>();

    public string? GetClaimValue(string claimType)
        => _httpContextAccessor.HttpContext?.User?.FindFirstValue(claimType);
}

using System.Security.Claims;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Learnexia.Modules.Curriculum.Infrastructure.Services;

/// <summary>
/// Reads the current authenticated user from request claims.
/// Mirrors Learning's <c>CurrentUserService</c> — registered in Curriculum Infrastructure
/// so the module can resolve <see cref="ICurrentUserService"/> without cross-module references.
/// (Module isolation: each module owns its own implementation of shared-kernel interfaces.)
/// </summary>
public class CurriculumCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurriculumCurrentUserService(IHttpContextAccessor httpContextAccessor)
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

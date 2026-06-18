using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace Learnexia.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Identity-side implementation of the cross-module <see cref="IChildGradeQuery"/> seam
/// (Shared.Contracts). Lets the Learning module's recommendation job read a student's grade
/// without referencing the Identity module directly or using the JWT (jobs have no HttpContext).
///
/// <para>Mirrors <see cref="UserLookup"/> shape: inject <c>UserManager&lt;User&gt;</c>, call
/// <c>FindByIdAsync</c>, return the shaped result. Scoped — UserManager is scoped.</para>
///
/// <para>P5-09-BE-3a.</para>
/// </summary>
public sealed class ChildGradeQueryService : IChildGradeQuery
{
    private readonly UserManager<User> _userManager;
    private readonly ILoggerManager _logger;

    public ChildGradeQueryService(
        UserManager<User> userManager,
        ILoggerManager logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int?> GetGradeAsync(int studentId, CancellationToken ct = default)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(studentId.ToString());
            return user?.Grade;
        }
        catch (Exception ex)
        {
            // Sentinel: never throws to callers — log and return null (job degrades gracefully).
            _logger.LogError(ex, $"IChildGradeQuery.GetGradeAsync failed for studentId={studentId}. Returning null.");
            return null;
        }
    }
}

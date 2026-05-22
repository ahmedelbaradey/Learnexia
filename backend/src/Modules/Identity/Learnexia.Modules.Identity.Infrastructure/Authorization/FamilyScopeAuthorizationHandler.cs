using System.Security.Claims;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Modules.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Identity.Infrastructure.Authorization;

/// <summary>
/// Resource-based handler for <see cref="FamilyScopeRequirement"/>. The resource is the target
/// <c>int studentId</c>. Succeeds when the caller:
///   • is Admin or SuperAdmin (full access); OR
///   • is the student themself (caller id == studentId, self-scope); OR
///   • is a parent with a <c>ParentStudent(parentId, studentId)</c> link row.
/// FAIL-CLOSED: <see cref="AuthorizationHandlerContext.Succeed"/> is called ONLY when one condition
/// holds. Every other path (no Id claim, unparsable id, no link) simply returns without succeeding,
/// so the framework denies by default. The DB check is fully awaited before any Succeed.
/// </summary>
public class FamilyScopeAuthorizationHandler : AuthorizationHandler<FamilyScopeRequirement, int>
{
    private readonly IdentityModuleDbContext _dbContext;

    public FamilyScopeAuthorizationHandler(IdentityModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        FamilyScopeRequirement requirement,
        int studentId)
    {
        // Admin / SuperAdmin → full access. Role claims are emitted as ClaimTypes.Role in PascalCase.
        if (context.User.IsInRole(Roles.Admin.ToString()) ||
            context.User.IsInRole(Roles.SuperAdmin.ToString()))
        {
            context.Succeed(requirement);
            return;
        }

        // Resolve the caller's user id from the "Id" claim. Fail closed if absent/unparsable.
        var idClaim = context.User.FindFirstValue(nameof(UserClaimsModel.Id));
        if (!int.TryParse(idClaim, out var callerId))
            return;

        // Self-scope: a student may access their own data.
        if (callerId == studentId)
        {
            context.Succeed(requirement);
            return;
        }

        // Parent-scope: succeed only if a link row exists for (callerId, studentId).
        var isLinkedParent = await _dbContext.ParentStudents
            .AnyAsync(ps => ps.ParentId == callerId && ps.StudentId == studentId);

        if (isLinkedParent)
            context.Succeed(requirement);

        // No else / no Fail() call — absence of Succeed lets the framework deny (fail-closed).
    }
}

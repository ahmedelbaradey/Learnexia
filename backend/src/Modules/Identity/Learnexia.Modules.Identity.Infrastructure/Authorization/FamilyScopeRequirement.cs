using Microsoft.AspNetCore.Authorization;

namespace Learnexia.Modules.Identity.Infrastructure.Authorization;

/// <summary>
/// Resource-based authorization requirement asserting the caller may act on a target student's data.
/// The resource passed to <c>IAuthorizationService.AuthorizeAsync(user, studentId, new FamilyScopeRequirement())</c>
/// is the target <c>int studentId</c>. Reused by P1-05 (RBAC family-scope) for any per-child endpoint.
/// Marker only — no state.
/// </summary>
public class FamilyScopeRequirement : IAuthorizationRequirement
{
}

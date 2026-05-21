namespace Learnexia.Shared.Kernel.Abstractions;

/// <summary>
/// Cross-cutting authorization policy names shared between the host's policy registration
/// (Identity Infrastructure) and the modules that decorate endpoints with them. Living in
/// Shared.Kernel keeps the policy string in one place without breaking module isolation
/// (a module never references another module's projects — CONVENTIONS §12).
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Requires the caller to hold the <c>Admin</c> or <c>SuperAdmin</c> role. Used to gate
    /// admin/observability lookups (e.g. notifications by recipient id). Registered in
    /// Identity Infrastructure's <c>AddAuthorization</c> via <c>RequireRole</c>.
    /// </summary>
    public const string AdminOnly = "AdminOnly";
}

namespace Learnexia.Modules.Identity.Domain.Enums;

/// <summary>
/// Governance lifecycle status for a user account.
/// Stored as int in the database; default 0 (Active) backfills all existing rows.
/// Distinct from <c>IsActive</c> (the mechanical sign-in gate, synced by handlers)
/// and from <c>LockoutEnd</c> (the temporary automatic failed-login lockout in P1-13).
/// </summary>
public enum AccountStatus
{
    /// <summary>Account is in good standing; sign-in is permitted.</summary>
    Active = 0,

    /// <summary>
    /// Admin has intentionally suspended the account.
    /// Sign-in is blocked; existing access tokens expire naturally (MVP residual window — G2 follow-up).
    /// Refresh token and tracked sessions are revoked on transition.
    /// </summary>
    Suspended = 1,

    /// <summary>
    /// Account has been soft-deleted by an admin.
    /// Terminal state — cannot be reactivated or re-suspended.
    /// Row is retained; PII anonymization is deferred to the full GDPR/COPPA sweep.
    /// </summary>
    Deleted = 2,
}

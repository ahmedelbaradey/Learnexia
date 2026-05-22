namespace Learnexia.Modules.Identity.Application.Features.Authentications.Queries.GetMe;

// Self-scoped current-user projection for the GET /api/Users/Me endpoint. Carries only identity
// fields needed to drive the client routing guard (role, language, onboarding state) — no password
// hashes, tokens, or internal audit fields. Every field is resolved server-side from the JWT-bound
// current user; the caller never supplies an id.
public class MeResponse
{
    public int Id { get; set; }

    // Roles as stored by Identity (PascalCase, e.g. "Parent" / "Student"); returned as-is.
    public IList<string> Roles { get; set; } = new List<string>();

    public string? FullName { get; set; }

    public string? PreferredLanguage { get; set; }

    // True until the user has completed registration/onboarding — mirrors SignIn's IsFirstLogin
    // (computed from RegistrationIsCompleted on the user record).
    public bool IsFirstLogin { get; set; }

    // True when the caller (a parent) has at least one linked child; always false for non-parents.
    public bool HasChildren { get; set; }
}

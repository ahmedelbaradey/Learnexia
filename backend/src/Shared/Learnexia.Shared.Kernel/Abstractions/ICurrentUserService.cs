namespace Learnexia.Shared.Kernel.Abstractions;

// Shared reader for the current authenticated user, sourced from the request's claims.
// Used by Identity (auth/session flows) and by module repositories for audit stamping.
public interface ICurrentUserService
{
    int? UserId { get; }
    string? UserName { get; }
    IList<string> Roles { get; }
    string? GetClaimValue(string claimType);
}

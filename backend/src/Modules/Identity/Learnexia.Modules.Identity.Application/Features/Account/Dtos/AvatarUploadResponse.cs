namespace Learnexia.Modules.Identity.Application.Features.Account.Dtos;

/// <summary>
/// Result of an avatar upload (P1-12 BE-4). The private bucket stores only the object KEY in
/// User.AvatarUrl; this carries a freshly presigned GET URL for the new object so the FE can render
/// the avatar without re-fetching the profile.
/// </summary>
public record AvatarUploadResponse
{
    public string? AvatarUrl { get; set; }
}

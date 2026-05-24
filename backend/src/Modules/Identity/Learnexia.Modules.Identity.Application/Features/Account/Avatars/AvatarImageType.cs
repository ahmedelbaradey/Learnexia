namespace Learnexia.Modules.Identity.Application.Features.Account.Avatars;

/// <summary>
/// Allowed avatar image formats (P1-12 BE-4). Fixed value set → enum (never magic strings). The
/// declared content-type AND the file's magic-byte signature must both resolve to one of these.
/// </summary>
public enum AvatarImageType
{
    Png,
    Jpeg,
    Webp
}

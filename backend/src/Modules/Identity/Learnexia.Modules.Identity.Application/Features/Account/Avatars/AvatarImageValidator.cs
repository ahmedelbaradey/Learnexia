using Microsoft.AspNetCore.Http;

namespace Learnexia.Modules.Identity.Application.Features.Account.Avatars;

/// <summary>
/// Validates that an uploaded avatar is genuinely a PNG/JPEG/WEBP by inspecting the magic-byte
/// signature (not only the declared content-type / extension), and that the declared content-type
/// matches one of the allowed formats (P1-12 BE-4). Size validation is enforced in the handler
/// against the configurable MaxFileSize. Technical identifiers (MIME types, extensions, byte
/// signatures) are protocol constants, not user-facing text.
/// </summary>
public static class AvatarImageValidator
{
    // Allowed declared content-types mapped to the format enum.
    private static readonly Dictionary<string, AvatarImageType> ContentTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = AvatarImageType.Png,
        ["image/jpeg"] = AvatarImageType.Jpeg,
        ["image/webp"] = AvatarImageType.Webp
    };

    /// <summary>True when the declared content-type is one of the allowed image MIME types.</summary>
    public static bool IsAllowedContentType(string? contentType)
        => contentType is not null && ContentTypeMap.ContainsKey(contentType);

    /// <summary>Maps an allowed format to its canonical file extension (including the dot).</summary>
    public static string GetExtension(AvatarImageType type) => type switch
    {
        AvatarImageType.Png => ".png",
        AvatarImageType.Jpeg => ".jpg",
        AvatarImageType.Webp => ".webp",
        _ => string.Empty
    };

    /// <summary>
    /// Maps a magic-byte-detected format to its canonical MIME content-type. Used to store the object
    /// with the TRUE detected content-type (not the client-declared MIME). Protocol constants.
    /// </summary>
    public static string GetContentType(AvatarImageType type) => type switch
    {
        AvatarImageType.Png => "image/png",
        AvatarImageType.Jpeg => "image/jpeg",
        AvatarImageType.Webp => "image/webp",
        _ => "application/octet-stream"
    };

    /// <summary>
    /// Reads the leading bytes of the stream and returns the detected image type from the magic-byte
    /// signature, or null if the content is not a supported image. Restores the stream position.
    /// </summary>
    public static async Task<AvatarImageType?> DetectByMagicBytesAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length < 12)
            return null;

        var header = new byte[12];
        await using (var stream = file.OpenReadStream())
        {
            var read = await ReadExactAsync(stream, header, cancellationToken);
            if (read < 12)
                return null;
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return AvatarImageType.Png;

        // JPEG: FF D8 FF
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return AvatarImageType.Jpeg;

        // WEBP: "RIFF"...."WEBP"
        if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return AvatarImageType.Webp;

        return null;
    }

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken);
            if (read == 0)
                break;
            total += read;
        }
        return total;
    }
}

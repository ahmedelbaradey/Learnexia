using Microsoft.AspNetCore.Http;

namespace Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Validators;

/// <summary>
/// Validates uploaded curriculum files by inspecting both the declared content-type (allow-list)
/// and the actual magic-byte signature (defends against spoofed content-types or extensions).
///
/// <para><strong>Extends the AvatarImageValidator pattern (BL-01 BE-3).</strong>
/// Adds support for PDF (<c>%PDF-</c>), DOCX (PK ZIP header <c>50 4B 03 04</c>),
/// and the image formats already covered by the avatar validator (JPEG/PNG/WEBP).
/// Uses hand-rolled byte-prefix checks — NO new NuGet package.</para>
///
/// <para><strong>DOCX residual-risk note (for security-auditor):</strong>
/// DOCX is a ZIP archive — the PK header (<c>50 4B 03 04</c>) matches any zip-based format
/// (e.g., OpenDocument ODS). The declared content-type
/// <c>application/vnd.openxmlformats-officedocument.wordprocessingml.document</c> is used as a
/// secondary guard, but cannot prevent a generic zip from being uploaded if the caller also
/// spoofs the content-type. Flag to the security-auditor for further review.</para>
///
/// <para>Technical identifiers (MIME strings, byte signatures) are protocol constants — not
/// user-facing text. Localization of validation errors happens in the handler.</para>
/// </summary>
public static class CurriculumFileValidator
{
    // Allowed declared content-types mapped to the file-type enum.
    private static readonly Dictionary<string, CurriculumFileType> ContentTypeMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = CurriculumFileType.Pdf,
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = CurriculumFileType.Docx,
            ["image/jpeg"] = CurriculumFileType.Jpeg,
            ["image/jpg"]  = CurriculumFileType.Jpeg,
            ["image/png"]  = CurriculumFileType.Png,
            ["image/webp"] = CurriculumFileType.Webp,
        };

    /// <summary>
    /// Returns true when the declared content-type is in the allowed set.
    /// </summary>
    public static bool IsAllowedContentType(string? contentType)
        => contentType is not null && ContentTypeMap.ContainsKey(contentType);

    /// <summary>
    /// Maps a detected file type to its canonical MIME content-type string.
    /// Used to store the object with the TRUE detected type (not the client-declared MIME).
    /// </summary>
    public static string GetContentType(CurriculumFileType type) => type switch
    {
        CurriculumFileType.Pdf  => "application/pdf",
        CurriculumFileType.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        CurriculumFileType.Jpeg => "image/jpeg",
        CurriculumFileType.Png  => "image/png",
        CurriculumFileType.Webp => "image/webp",
        _                       => "application/octet-stream",
    };

    /// <summary>
    /// Maps a detected file type to its canonical file extension (including the dot).
    /// Used to build the GUID-based object key without path traversal.
    /// </summary>
    public static string GetExtension(CurriculumFileType type) => type switch
    {
        CurriculumFileType.Pdf  => ".pdf",
        CurriculumFileType.Docx => ".docx",
        CurriculumFileType.Jpeg => ".jpg",
        CurriculumFileType.Png  => ".png",
        CurriculumFileType.Webp => ".webp",
        _                       => string.Empty,
    };

    /// <summary>
    /// Reads the leading bytes of the stream and returns the detected file type from the
    /// magic-byte signature, or <c>null</c> if the content is not a supported curriculum file.
    /// Restores the stream position so the caller can read the full file afterward.
    ///
    /// <para>Requires at least 12 bytes to resolve WEBP (RIFF....WEBP header).</para>
    /// </summary>
    public static async Task<CurriculumFileType?> DetectByMagicBytesAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length < 5)
            return null;

        // Read 12 bytes — enough to resolve PDF (5), DOCX/ZIP (4), JPEG (3), PNG (8), WEBP (12).
        var header = new byte[12];
        await using (var stream = file.OpenReadStream())
        {
            var read = await ReadExactAsync(stream, header, cancellationToken);
            if (read < 4)
                return null;
        }

        // PDF: 25 50 44 46 2D (%PDF-)
        if (header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 &&
            header[3] == 0x46 && header[4] == 0x2D)
            return CurriculumFileType.Pdf;

        // DOCX (ZIP/PK header): 50 4B 03 04
        // Security note: PK header matches any ZIP-based format (OOXML, ODS, etc.).
        // Declared content-type is used as a secondary guard in the handler. Flag to security-auditor.
        if (header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04)
            return CurriculumFileType.Docx;

        // JPEG: FF D8 FF
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return CurriculumFileType.Jpeg;

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return CurriculumFileType.Png;

        // WEBP: "RIFF" (52 49 46 46) at bytes 0-3, then "WEBP" (57 45 42 50) at bytes 8-11.
        if (header.Length >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return CurriculumFileType.Webp;

        return null;
    }

    private static async Task<int> ReadExactAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(
                buffer.AsMemory(total, buffer.Length - total),
                cancellationToken);
            if (read == 0)
                break;
            total += read;
        }
        return total;
    }
}

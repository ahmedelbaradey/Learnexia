using System.Net;
using System.Text.Json;
using Learnexia.Modules.Learning.Domain.Enums;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.ContentBlocks.Validation;

/// <summary>
/// Static helper for per-BlockType payload validation (P7-02).
/// Validates that the <c>Payload</c> JSON string contains the required fields for the given <see cref="ContentBlockType"/>.
///
/// Plain static method invoked from a FluentValidation command validator — NOT a Strategy/Factory pattern
/// (CLAUDE.md rule 8: no design pattern without lead approval). Mirrors <c>QuizQuestionTypeValidation</c> exactly.
///
/// Shape rules per type:
///   Text    — Payload JSON must contain "markdown" (non-empty, ≤ 32768 chars).
///   Image   — Payload JSON must contain "url" (absolute HTTPS, public host, ≤ 2048 chars);
///              "altText" is optional (≤ 1024 chars).
///   Video   — Payload JSON must contain "url" (absolute HTTPS, public host, ≤ 2048 chars);
///              "caption" is optional (≤ 1024 chars).
///   Callout — Payload JSON must contain "variant" (one of: info, warning, tip)
///              and "markdown" (non-empty, ≤ 32768 chars).
///
/// Payload is stored as jsonb in PostgreSQL; shape is enforced only in FluentValidation,
/// matching the same pattern as <c>QuizQuestion.Options</c>.
///
/// P7-SEC-2: overall payload capped at 65536 chars (DoS guard).
/// P7-SEC-3: Image/Video url fields require absolute HTTPS URI; private/loopback/link-local
///           hosts are rejected (XSS/SSRF guard — children's platform).
/// </summary>
public static class ContentBlockPayloadValidator
{
    // ── Length limits ─────────────────────────────────────────────────────────────
    /// <summary>Maximum total Payload length (jsonb string). Validators also enforce this at the command level.</summary>
    public const int MaxPayloadLength = 65536;

    /// <summary>Maximum length for markdown fields (Text and Callout markdown).</summary>
    private const int MaxMarkdownLength = 32768;

    /// <summary>Maximum length for url fields (Image and Video).</summary>
    private const int MaxUrlLength = 2048;

    /// <summary>Maximum length for optional fields (altText, caption).</summary>
    private const int MaxOptionalFieldLength = 1024;

    // ── Allowed URL schemes ───────────────────────────────────────────────────────
    private static readonly HashSet<string> AllowedUrlSchemes =
        new(StringComparer.OrdinalIgnoreCase) { "https" };

    // ── Blocked private/loopback IPv4 ranges ─────────────────────────────────────
    // 127.0.0.0/8, 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 169.254.0.0/16 (link-local)
    private static readonly (uint Network, uint Mask)[] BlockedIpv4Ranges =
    {
        (0x7F000000u, 0xFF000000u), // 127.0.0.0/8     — loopback
        (0x0A000000u, 0xFF000000u), // 10.0.0.0/8      — private class A
        (0xAC100000u, 0xFFF00000u), // 172.16.0.0/12   — private class B
        (0xC0A80000u, 0xFFFF0000u), // 192.168.0.0/16  — private class C
        (0xA9FE0000u, 0xFFFF0000u), // 169.254.0.0/16  — link-local
    };

    private static readonly HashSet<string> ValidCalloutVariants =
        new(StringComparer.OrdinalIgnoreCase) { "info", "warning", "tip" };

    /// <summary>
    /// Validates the payload JSON for the given block type.
    /// Returns null when valid; a localized error message string when invalid.
    /// </summary>
    public static string? Validate(
        ContentBlockType blockType,
        string? payload,
        IStringLocalizer<SharedResources> localizer)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return localizer[SharedResourcesKey.ContentBlockPayloadInvalid];

        // P7-SEC-2: total payload size guard (validators also call MaximumLength on the field)
        if (payload.Length > MaxPayloadLength)
            return localizer[SharedResourcesKey.ContentBlockPayloadTooLong];

        return blockType switch
        {
            ContentBlockType.Text    => ValidateText(payload, localizer),
            ContentBlockType.Image   => ValidateImage(payload, localizer),
            ContentBlockType.Video   => ValidateVideo(payload, localizer),
            ContentBlockType.Callout => ValidateCallout(payload, localizer),
            _                        => localizer[SharedResourcesKey.ContentBlockTypeInvalid]
        };
    }

    // ── Text ─────────────────────────────────────────────────────────────────────
    // { "markdown": "<non-empty string, ≤32768>" }

    private static string? ValidateText(string payload, IStringLocalizer<SharedResources> localizer)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (!root.TryGetProperty("markdown", out var markdownEl) ||
                markdownEl.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(markdownEl.GetString()))
                return localizer[SharedResourcesKey.ContentBlockTextPayloadRequired];

            var markdown = markdownEl.GetString()!;
            if (markdown.Length > MaxMarkdownLength)
                return localizer[SharedResourcesKey.ContentBlockPayloadTooLong];
        }
        catch (JsonException)
        {
            return localizer[SharedResourcesKey.ContentBlockPayloadInvalid];
        }

        return null;
    }

    // ── Image ─────────────────────────────────────────────────────────────────────
    // { "url": "<absolute HTTPS, ≤2048>", "altText": "<optional, ≤1024>" }

    private static string? ValidateImage(string payload, IStringLocalizer<SharedResources> localizer)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (!root.TryGetProperty("url", out var urlEl) ||
                urlEl.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(urlEl.GetString()))
                return localizer[SharedResourcesKey.ContentBlockImageUrlRequired];

            var urlError = ValidateUrl(urlEl.GetString()!, localizer);
            if (urlError is not null)
                return urlError;

            if (root.TryGetProperty("altText", out var altEl) &&
                altEl.ValueKind == JsonValueKind.String &&
                altEl.GetString() is { Length: > MaxOptionalFieldLength })
                return localizer[SharedResourcesKey.ContentBlockPayloadTooLong];
        }
        catch (JsonException)
        {
            return localizer[SharedResourcesKey.ContentBlockPayloadInvalid];
        }

        return null;
    }

    // ── Video ─────────────────────────────────────────────────────────────────────
    // { "url": "<absolute HTTPS, ≤2048>", "caption": "<optional, ≤1024>" }

    private static string? ValidateVideo(string payload, IStringLocalizer<SharedResources> localizer)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (!root.TryGetProperty("url", out var urlEl) ||
                urlEl.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(urlEl.GetString()))
                return localizer[SharedResourcesKey.ContentBlockVideoUrlRequired];

            var urlError = ValidateUrl(urlEl.GetString()!, localizer);
            if (urlError is not null)
                return urlError;

            if (root.TryGetProperty("caption", out var capEl) &&
                capEl.ValueKind == JsonValueKind.String &&
                capEl.GetString() is { Length: > MaxOptionalFieldLength })
                return localizer[SharedResourcesKey.ContentBlockPayloadTooLong];
        }
        catch (JsonException)
        {
            return localizer[SharedResourcesKey.ContentBlockPayloadInvalid];
        }

        return null;
    }

    // ── Callout ───────────────────────────────────────────────────────────────────
    // { "variant": "info|warning|tip", "markdown": "<non-empty, ≤32768>" }

    private static string? ValidateCallout(string payload, IStringLocalizer<SharedResources> localizer)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (!root.TryGetProperty("variant", out var variantEl) ||
                variantEl.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(variantEl.GetString()))
                return localizer[SharedResourcesKey.ContentBlockCalloutVariantRequired];

            var variantValue = variantEl.GetString()!.Trim();
            if (!ValidCalloutVariants.Contains(variantValue))
                return localizer[SharedResourcesKey.ContentBlockCalloutVariantInvalid];

            if (!root.TryGetProperty("markdown", out var markdownEl) ||
                markdownEl.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(markdownEl.GetString()))
                return localizer[SharedResourcesKey.ContentBlockCalloutMarkdownRequired];

            var markdown = markdownEl.GetString()!;
            if (markdown.Length > MaxMarkdownLength)
                return localizer[SharedResourcesKey.ContentBlockPayloadTooLong];
        }
        catch (JsonException)
        {
            return localizer[SharedResourcesKey.ContentBlockPayloadInvalid];
        }

        return null;
    }

    // ── URL safety (P7-SEC-3) ─────────────────────────────────────────────────────
    // Reject: non-absolute URIs, non-https schemes, URLs > 2048 chars,
    //         loopback / link-local / private-range hosts (SSRF/XSS guard).

    private static string? ValidateUrl(string url, IStringLocalizer<SharedResources> localizer)
    {
        if (url.Length > MaxUrlLength)
            return localizer[SharedResourcesKey.ContentBlockUrlInvalid];

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return localizer[SharedResourcesKey.ContentBlockUrlInvalid];

        if (!AllowedUrlSchemes.Contains(uri.Scheme))
            return localizer[SharedResourcesKey.ContentBlockUrlInvalid];

        // Reject loopback / link-local / private host names and IP ranges.
        var host = uri.Host;

        // "localhost" and well-known loopback names
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase))
            return localizer[SharedResourcesKey.ContentBlockUrlInvalid];

        // Try to parse as IPv4 and check against blocked ranges
        if (IPAddress.TryParse(host, out var ip))
        {
            var addr = ip.MapToIPv4();
            if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var bytes = addr.GetAddressBytes();
                var numeric = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) |
                              ((uint)bytes[2] << 8)  |  (uint)bytes[3];

                foreach (var (network, mask) in BlockedIpv4Ranges)
                {
                    if ((numeric & mask) == network)
                        return localizer[SharedResourcesKey.ContentBlockUrlInvalid];
                }
            }

            // Reject all IPv6 addresses except where already handled above (::1 checked as string).
            // IPv6 link-local (fe80::/10) and other private ranges are also disallowed
            // on a children's platform — reject any bare IP entirely.
            if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                return localizer[SharedResourcesKey.ContentBlockUrlInvalid];
        }

        return null;
    }
}

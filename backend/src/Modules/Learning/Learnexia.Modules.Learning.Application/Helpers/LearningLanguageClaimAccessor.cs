using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;

namespace Learnexia.Modules.Learning.Application.Helpers;

/// <summary>
/// Named constant and typed accessor for the <c>learning_language</c> JWT claim.
///
/// P8-03-BE-1: Reads <c>learning_language</c> from the authenticated principal via
/// <see cref="ICurrentUserService.GetClaimValue"/>. Parses "ar"/"en" to <see cref="ContentLanguage"/>.
/// Absent or unrecognised claim → fallback <see cref="ContentLanguage.Ar"/> (Arabic-first product
/// default, per lead decision §3). The caller must supply an <see cref="ILoggerManager"/> to emit
/// the warning — the accessor never throws.
///
/// Uses the same claim-read seam that <c>Learning.Infrastructure.Service.CurrentUserService</c>
/// already uses for the student Id claim — no new plumbing required.
/// </summary>
public static class LearningLanguageClaimAccessor
{
    /// <summary>
    /// Named constant for the JWT claim type. Matches the claim emitted by
    /// <c>AuthenticationIdentityService.GetClaims</c> (P8-01-BE-4).
    /// </summary>
    public const string ClaimType = "learning_language";

    /// <summary>
    /// Reads the <c>learning_language</c> claim and returns the parsed <see cref="ContentLanguage"/>.
    /// Falls back to <see cref="ContentLanguage.Ar"/> and logs a warning when the claim is absent or
    /// unrecognised (never returns null, never throws).
    /// </summary>
    /// <param name="currentUser">The current-user service (HTTP context claims).</param>
    /// <param name="logger">Logger used to emit the fallback warning.</param>
    /// <returns>Parsed <see cref="ContentLanguage"/> or <see cref="ContentLanguage.Ar"/> as default.</returns>
    public static ContentLanguage GetLearningLanguage(ICurrentUserService currentUser, ILoggerManager logger)
    {
        var raw = currentUser.GetClaimValue(ClaimType);

        if (raw == "ar")
            return ContentLanguage.Ar;

        if (raw == "en")
            return ContentLanguage.En;

        // Claim absent (legacy token) or unrecognised value — fallback to Arabic-first default.
        // P8-SEC-4: sanitize the raw claim value before logging to close the log-injection vector.
        // Strip CR/LF and truncate to 10 chars so a crafted claim cannot inject fake log lines.
        string safeRaw;
        if (raw is null)
        {
            safeRaw = "<null>";
        }
        else
        {
            var stripped = raw.Replace("\r", string.Empty, StringComparison.Ordinal)
                              .Replace("\n", string.Empty, StringComparison.Ordinal);
            safeRaw = stripped.Length > 10 ? stripped[..10] : stripped;
        }
        logger.LogWarn(
            $"JWT claim '{ClaimType}' is absent or unrecognised (value='{safeRaw}')." +
            " Defaulting to ContentLanguage.Ar per product fallback rule.");

        return ContentLanguage.Ar;
    }
}

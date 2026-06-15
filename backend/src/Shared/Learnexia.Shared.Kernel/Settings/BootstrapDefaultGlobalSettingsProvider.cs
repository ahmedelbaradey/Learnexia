using Microsoft.Extensions.Configuration;

namespace Learnexia.Shared.Kernel.Settings;

/// <summary>
/// Bootstrap implementation of <see cref="IGlobalSettingsProvider"/> that returns values from
/// <c>appsettings.json</c> (section <c>AiHelper:Cache:*</c> and siblings) with a code-level
/// fallback when a key is absent or unparseable.
///
/// <para>This is the Phase-4 default. The P10-12 upgrade (Phase 10) replaces this with a
/// DB-backed, Redis-cached, admin-editable implementation — callers need no change.</para>
///
/// <para>Key mapping convention: dotted key paths (e.g. <c>"ai.cache.autoApprovalConfidence"</c>)
/// are mapped to their <c>IConfiguration</c> equivalent by replacing <c>.</c> with <c>:</c>
/// (the standard <c>IConfiguration</c> path separator).</para>
/// </summary>
public sealed class BootstrapDefaultGlobalSettingsProvider : IGlobalSettingsProvider
{
    private readonly IConfiguration _configuration;

    public BootstrapDefaultGlobalSettingsProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <inheritdoc/>
    public decimal GetDecimal(string key, decimal defaultValue)
    {
        var raw = _configuration[ToConfigPath(key)];
        if (raw is null) return defaultValue;
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;
    }

    /// <inheritdoc/>
    public int GetInt(string key, int defaultValue)
    {
        var raw = _configuration[ToConfigPath(key)];
        if (raw is null) return defaultValue;
        return int.TryParse(raw, out var parsed) ? parsed : defaultValue;
    }

    /// <inheritdoc/>
    public string GetString(string key, string defaultValue)
    {
        var raw = _configuration[ToConfigPath(key)];
        return string.IsNullOrEmpty(raw) ? defaultValue : raw;
    }

    /// <inheritdoc/>
    public bool GetBool(string key, bool defaultValue)
    {
        var raw = _configuration[ToConfigPath(key)];
        if (raw is null) return defaultValue;
        return bool.TryParse(raw, out var parsed) ? parsed : defaultValue;
    }

    /// <summary>
    /// Converts a dotted setting key (e.g. <c>"ai.cache.autoApprovalConfidence"</c>) to the
    /// colon-separated IConfiguration path (e.g. <c>"AiHelper:Cache:autoApprovalConfidence"</c>).
    ///
    /// <para>Mapping rules (kept simple for the bootstrap impl — P10-12 will have a full registry):
    /// prefix <c>ai.cache.</c> → <c>AiHelper:Cache:</c>; otherwise replace <c>.</c> with <c>:</c>.</para>
    /// </summary>
    private static string ToConfigPath(string key)
    {
        // Canonical prefix mappings for Phase-4 cache/review keys.
        if (key.StartsWith("ai.cache.", StringComparison.OrdinalIgnoreCase))
            return "AiHelper:Cache:" + key["ai.cache.".Length..];

        // Fallback: simple dot → colon substitution (IConfiguration convention).
        return key.Replace('.', ':');
    }
}

namespace Learnexia.Shared.Kernel.Settings;

/// <summary>
/// Runtime global settings seam. Returns config-bound values with typed accessors and a
/// caller-supplied fallback when the key is absent.
///
/// <para>Bootstrap implementation (<c>BootstrapDefaultGlobalSettingsProvider</c> in the same
/// namespace) returns code / appsettings defaults for every key. The Phase-10 P10-12 upgrade
/// replaces the implementation with a DB-backed, Redis-cached, admin-editable store — no
/// calling-code change is required.</para>
///
/// <para>Callers MUST use this seam for all tunable runtime thresholds (cache TTLs, model
/// routing economy values, approval confidence, etc.) — never read from
/// <c>AiGatewayOptions.*</c>, a hardcoded constant, or <c>appsettings.json</c> directly.
/// See <c>ai-cost-routing.md §8b</c>.</para>
///
/// <para>Placed in <c>Shared.Kernel</c> so the bootstrap implementation can live in the same
/// project without creating a circular dependency (<c>Shared.Contracts</c> does NOT depend
/// on <c>Shared.Kernel</c>).</para>
/// </summary>
public interface IGlobalSettingsProvider
{
    /// <summary>Returns a <see cref="decimal"/> setting value for <paramref name="key"/>,
    /// or <paramref name="defaultValue"/> when the key is absent or unparseable.</summary>
    decimal GetDecimal(string key, decimal defaultValue);

    /// <summary>Returns an <see cref="int"/> setting value for <paramref name="key"/>,
    /// or <paramref name="defaultValue"/> when the key is absent or unparseable.</summary>
    int GetInt(string key, int defaultValue);

    /// <summary>Returns a <see cref="string"/> setting value for <paramref name="key"/>,
    /// or <paramref name="defaultValue"/> when the key is absent.</summary>
    string GetString(string key, string defaultValue);

    /// <summary>Returns a <see cref="bool"/> setting value for <paramref name="key"/>,
    /// or <paramref name="defaultValue"/> when the key is absent or unparseable.</summary>
    bool GetBool(string key, bool defaultValue);
}

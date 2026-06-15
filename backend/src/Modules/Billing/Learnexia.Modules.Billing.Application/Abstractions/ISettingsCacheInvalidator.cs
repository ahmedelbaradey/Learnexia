namespace Learnexia.Modules.Billing.Application.Abstractions;

/// <summary>
/// Internal seam for invalidating the in-memory (+ Redis) settings cache after an admin write.
///
/// <para>Implemented by <c>DbBackedGlobalSettingsProvider</c> in <c>Billing.Infrastructure</c>.
/// Separated from <see cref="Learnexia.Shared.Kernel.Settings.IGlobalSettingsProvider"/> so the
/// public interface remains unchanged (DRIFT-1 LOCKED decision).</para>
/// </summary>
public interface ISettingsCacheInvalidator
{
    /// <summary>
    /// Removes the cache entry for <paramref name="key"/> so the next read reloads from DB.
    /// Must not throw — absorbs failures internally.
    /// </summary>
    void InvalidateKey(string key);
}

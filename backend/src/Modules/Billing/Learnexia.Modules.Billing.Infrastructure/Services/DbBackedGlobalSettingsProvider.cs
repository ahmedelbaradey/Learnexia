using System.Collections.Concurrent;
using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// DB-backed implementation of <see cref="IGlobalSettingsProvider"/> that:
/// <list type="bullet">
///   <item>Reads from <c>platform.GlobalSettings</c> via <c>BillingDbContext</c>.</item>
///   <item>Caches all values in an in-memory <see cref="ConcurrentDictionary"/> (Singleton lifetime).</item>
///   <item>Falls back to the caller-supplied <paramref name="defaultValue"/> when a key is absent
///         from DB (bootstrap-compatible with Phase-4 callers — DRIFT-1 LOCKED).</item>
///   <item>Exposes <see cref="InvalidateKey"/> via <see cref="ISettingsCacheInvalidator"/> so
///         <c>UpdateGlobalSettingCommandHandler</c> can flush a single entry after a write
///         (without touching the public <c>IGlobalSettingsProvider</c> signature — LOCKED).</item>
/// </list>
///
/// <para>Registered as <strong>Singleton</strong> in <c>AddBillingInfrastructure</c>, overriding
/// <c>BootstrapDefaultGlobalSettingsProvider</c>. Reads from DB are done on a fresh DI scope
/// (factory pattern) so a Singleton can safely use Scoped <c>BillingDbContext</c>.</para>
/// </summary>
public sealed class DbBackedGlobalSettingsProvider : IGlobalSettingsProvider, ISettingsCacheInvalidator
{
    // In-memory cache: key → string value.  Loaded lazily per key; invalidated on admin write.
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.Ordinal);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerManager _logger;

    public DbBackedGlobalSettingsProvider(IServiceScopeFactory scopeFactory, ILoggerManager logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    // ── IGlobalSettingsProvider ──────────────────────────────────────────────

    public decimal GetDecimal(string key, decimal defaultValue)
    {
        var raw = GetRaw(key);
        if (raw is null) return defaultValue;
        return decimal.TryParse(raw,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed) ? parsed : defaultValue;
    }

    public int GetInt(string key, int defaultValue)
    {
        var raw = GetRaw(key);
        if (raw is null) return defaultValue;
        return int.TryParse(raw, out var parsed) ? parsed : defaultValue;
    }

    public string GetString(string key, string defaultValue)
    {
        var raw = GetRaw(key);
        return string.IsNullOrEmpty(raw) ? defaultValue : raw;
    }

    public bool GetBool(string key, bool defaultValue)
    {
        var raw = GetRaw(key);
        if (raw is null) return defaultValue;
        return bool.TryParse(raw, out var parsed) ? parsed : defaultValue;
    }

    // ── ISettingsCacheInvalidator ────────────────────────────────────────────

    /// <summary>
    /// Removes the cached value for <paramref name="key"/>. The next call to any getter
    /// for that key will reload from DB. Safe to call from a non-Singleton scope.
    /// </summary>
    public void InvalidateKey(string key)
    {
        _cache.TryRemove(key, out _);
        _logger.LogInfo($"DbBackedGlobalSettingsProvider: cache invalidated for key={key}.");
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns the raw string value for <paramref name="key"/> from the in-memory cache,
    /// loading from DB on a cache miss. Returns <c>null</c> if the key is absent from DB.
    /// </summary>
    private string? GetRaw(string key)
    {
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        // Cache miss — load from DB on a fresh scope (Singleton-safe).
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

            var row = db.GlobalSettings.AsNoTracking()
                .FirstOrDefault(s => s.Key == key);

            if (row is null)
            {
                _logger.LogInfo($"DbBackedGlobalSettingsProvider: key={key} not in DB — using caller default.");
                return null;
            }

            // Store in cache.
            _cache[key] = row.Value;
            return row.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarn($"DbBackedGlobalSettingsProvider: DB read failed for key={key}, returning null (caller default). {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Warm-up: loads all rows from <c>platform.GlobalSettings</c> into the in-memory cache.
    /// Called by <see cref="GlobalSettingsWarmupService"/> at startup so the first live request
    /// does not incur a DB round-trip per key.
    /// </summary>
    public void WarmUp()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

            var rows = db.GlobalSettings.AsNoTracking().ToList();
            foreach (var row in rows)
                _cache[row.Key] = row.Value;

            _logger.LogInfo($"DbBackedGlobalSettingsProvider: warm-up complete — {rows.Count} settings cached.");
        }
        catch (Exception ex)
        {
            _logger.LogWarn($"DbBackedGlobalSettingsProvider: warm-up failed — settings will be loaded lazily. {ex.GetType().Name}: {ex.Message}");
        }
    }
}

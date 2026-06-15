using Learnexia.Modules.Billing.Domain.Enums;

namespace Learnexia.Modules.Billing.Domain.Entities;

/// <summary>
/// A single runtime-configurable platform setting stored in <c>platform.GlobalSettings</c>.
///
/// <para><c>Key</c> is the primary key (natural key, varchar 200). <c>Value</c> is always stored as
/// a string and parsed to the target type at read-time by <c>DbBackedGlobalSettingsProvider</c>.</para>
///
/// <para>No public setters — all mutations go through <see cref="Update"/>.</para>
/// </summary>
public sealed class GlobalSetting
{
    private GlobalSetting() { }

    /// <summary>Creates a new <see cref="GlobalSetting"/> with the given defaults (used by the seeder).</summary>
    public static GlobalSetting Create(string key, string value, GlobalSettingType type, string updatedBy, DateTime updatedAt)
        => new()
        {
            Key       = key,
            Value     = value,
            Type      = type,
            UpdatedBy = updatedBy,
            UpdatedAt = updatedAt,
        };

    /// <summary>Setting key — PK, max 200 chars (dotted path, e.g. <c>ai_cost.hint</c>).</summary>
    public string Key { get; private set; } = default!;

    /// <summary>Value serialized as a string; parsed on read by the provider.</summary>
    public string Value { get; private set; } = default!;

    /// <summary>Declared type — used by the validator and provider to parse/validate.</summary>
    public GlobalSettingType Type { get; private set; }

    /// <summary>Identity string of the admin who last changed this row (no FK — module-isolated).</summary>
    public string UpdatedBy { get; private set; } = default!;

    /// <summary>Timestamp of the last update (UTC).</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Updates the value + audit fields. Called from <c>UpdateGlobalSettingCommandHandler</c>.
    /// </summary>
    public void Update(string newValue, string updatedBy, DateTime updatedAt)
    {
        Value     = newValue;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt;
    }
}

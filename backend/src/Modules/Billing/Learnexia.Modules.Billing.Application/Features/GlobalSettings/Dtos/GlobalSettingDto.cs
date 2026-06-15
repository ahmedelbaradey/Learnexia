using Learnexia.Modules.Billing.Domain.Enums;

namespace Learnexia.Modules.Billing.Application.Features.GlobalSettings.Dtos;

/// <summary>A single managed global setting as returned by <c>GetGlobalSettingsQuery</c>.</summary>
public sealed class GlobalSettingDto
{
    public string Key       { get; set; } = default!;
    public string Value     { get; set; } = default!;
    public GlobalSettingType Type { get; set; }
    public string UpdatedBy { get; set; } = default!;
    public DateTime UpdatedAt { get; set; }
}

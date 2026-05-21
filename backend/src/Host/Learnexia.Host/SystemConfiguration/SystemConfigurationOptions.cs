namespace Learnexia.Host.SystemConfiguration;

public sealed class SystemConfigurationOptions
{
    public const string SectionName = "SystemConfiguration";

    public string ApplicationName { get; set; } = "Learnexia";
    public string DefaultCulture { get; set; } = "en";
    public bool MaintenanceMode { get; set; }
}

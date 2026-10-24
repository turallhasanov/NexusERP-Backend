namespace NexusERP.Application.SystemInfo;

/// <summary>
/// "SystemInfo" section of appsettings: the parts of the splash's about box that depend on the deployment.
/// List an integration here only once it is actually running; the splash shows it as-is.
/// </summary>
public sealed class SystemInfoSettings
{
    public const string SectionName = "SystemInfo";

    public List<IntegrationSettings> Integrations { get; set; } = [];

    public string DeveloperNote { get; set; } = string.Empty;
}

public sealed class IntegrationSettings
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Free text shown next to the name, e.g. "Active" or "Paused".</summary>
    public string Status { get; set; } = string.Empty;
}

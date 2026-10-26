using NexusERP.Application.Common;

namespace NexusERP.Application.SystemInfo;

public sealed record IntegrationDto(string Name, string Status);

/// <summary>
/// GET /api/system/info → { "ok": true, "appName", "version", "environment", "copyright", "architectureNote",
/// "architecture": [..], "integrations": [{ "name", "status" }], "developerNote" }
/// </summary>
public sealed record SystemInfoResponseDto(
    string AppName,
    string Version,
    string Environment,
    string Copyright,
    string ArchitectureNote,
    IReadOnlyList<string> Architecture,
    IReadOnlyList<IntegrationDto> Integrations,
    string DeveloperNote) : SuccessResponse;

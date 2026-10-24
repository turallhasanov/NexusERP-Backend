using Microsoft.Extensions.Options;
using NexusERP.Application.Common;

namespace NexusERP.Application.SystemInfo;

/// <summary>Public application facts shown on the frontend's splash screen. Nothing here may be secret.</summary>
public class SystemInfoService(IOptions<SystemInfoSettings> options) : ISystemInfoService
{
    private const string AppName = "NexusERP Enterprise";
    private const string Version = "v1.0.0";
    private const string Copyright = "© 2026 NexusERP Inc. All rights reserved.";
    private const string ArchitectureNote = "Powered by Clean Architecture & .NET 8";

    // Facts about the codebase itself, so they live here rather than in configuration.
    private static readonly string[] Architecture =
    [
        ".NET 8 Clean Architecture (Domain · Application · Infrastructure · API)",
        "Multi-tenant: every record is scoped to its company via the JWT companyId claim",
        "JWT authentication with e-mail OTP verification"
    ];

    public Result<SystemInfoResponseDto> GetInfo(string environmentName)
    {
        var settings = options.Value;
        var integrations = settings.Integrations
            .Where(i => !string.IsNullOrWhiteSpace(i.Name))
            .Select(i => new IntegrationDto(i.Name.Trim(), i.Status.Trim()))
            .ToList();

        return new SystemInfoResponseDto(
            AppName, Version, environmentName, Copyright, ArchitectureNote,
            Architecture, integrations, settings.DeveloperNote.Trim());
    }
}

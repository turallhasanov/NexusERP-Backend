using NexusERP.Application.Common;

namespace NexusERP.Application.SystemInfo;

public interface ISystemInfoService
{
    /// <param name="environmentName">The host environment ("Production", "Development", ...); the API layer owns it.</param>
    Result<SystemInfoResponseDto> GetInfo(string environmentName);
}

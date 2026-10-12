using NexusERP.Application.Common;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Auth;

/// <summary>Fields are nullable so missing values reach our own validation instead of the framework's.</summary>
public sealed record LoginRequestDto(string? Email, string? Password);

public sealed record UserDto(string Id, string Name, string Role, string? CompanyId)
{
    public static UserDto From(User user) => new(user.Id, user.Name, user.Role, user.CompanyId);
}

/// <summary>{ "ok": true, "token": "...", "user": { ... } }</summary>
public sealed record AuthResponseDto(string Token, UserDto User) : SuccessResponse;

/// <summary>{ "ok": true, "user": { ... } }</summary>
public sealed record CurrentUserResponseDto(UserDto User) : SuccessResponse;

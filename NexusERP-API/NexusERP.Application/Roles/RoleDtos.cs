using NexusERP.Application.Common;

namespace NexusERP.Application.Roles;

/// <summary>Body of POST /api/roles. Fields are nullable so missing values reach our own validation.</summary>
public sealed record CreateRoleRequestDto(string? Name, string? Description, IReadOnlyList<string>? Permissions);

/// <summary>Body of PUT /api/roles/{id}: the role's new name, description and full permission list.</summary>
public sealed record UpdateRoleRequestDto(string? Name, string? Description, IReadOnlyList<string>? Permissions);

/// <summary>Body of POST /api/users/{userId}/assign-role: { "roleId": "..." }, or null to take the role away.</summary>
public sealed record AssignRoleRequestDto(string? RoleId);

/// <summary>A company's custom role with the number of users who have it.</summary>
public sealed record RoleDto(
    string Id, string Name, string? Description, IReadOnlyList<string> Permissions, int UserCount, DateTime CreatedAt);

/// <summary>POST /api/roles and PUT /api/roles/{id} → { "ok": true, "role": { ... } }</summary>
public sealed record RoleResponseDto(RoleDto Role) : SuccessResponse;

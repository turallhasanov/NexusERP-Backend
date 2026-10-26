using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Application.Companies;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Roles;

public class RoleService(IApplicationDbContext db) : IRoleService
{
    // Must match the column lengths in CustomRoleConfiguration.
    private const int NameMaxLength = 100;
    private const int DescriptionMaxLength = 500;

    private const string RoleNotFound = "Rol tapılmadı.";
    private const string NameTaken = "Bu adda rol artıq mövcuddur.";
    private const string RoleInUse = "Bu rola bağlı istifadəçi var, silinə bilməz.";

    private static readonly StringComparer NameComparer = StringComparer.Create(CultureInfo.GetCultureInfo("az"), ignoreCase: true);

    public async Task<Result<ItemsResponseDto<RoleDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken)
    {
        var roles = await db.CustomRoles
            .AsNoTracking()
            .Where(r => r.CompanyId == companyId)
            .OrderBy(r => r.Name)
            .Select(r => new
            {
                Role = r,
                UserCount = db.Users.Count(u => u.CustomRoleId == r.Id),
            })
            .ToListAsync(cancellationToken);

        return new ItemsResponseDto<RoleDto>(roles.Select(r => ToDto(r.Role, r.UserCount)).ToList());
    }

    public async Task<Result<RoleResponseDto>> CreateAsync(string companyId, CreateRoleRequestDto request, CancellationToken cancellationToken)
    {
        var input = Normalize(request.Name, request.Description, request.Permissions);

        if (input.Error is not null)
            return Result<RoleResponseDto>.Failure(ErrorType.Validation, input.Error);

        if (await NameExistsAsync(companyId, input.Name, exceptId: null, cancellationToken))
            return Result<RoleResponseDto>.Failure(ErrorType.Conflict, NameTaken);

        var role = new CustomRole
        {
            CompanyId = companyId,
            Name = input.Name,
            Description = input.Description,
            Permissions = input.Permissions,
        };

        db.CustomRoles.Add(role);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another request created the same name since the check above (unique index).
            return Result<RoleResponseDto>.Failure(ErrorType.Conflict, NameTaken);
        }

        return new RoleResponseDto(ToDto(role, 0));
    }

    public async Task<Result<RoleResponseDto>> UpdateAsync(string companyId, string id, UpdateRoleRequestDto request, CancellationToken cancellationToken)
    {
        var input = Normalize(request.Name, request.Description, request.Permissions);

        if (input.Error is not null)
            return Result<RoleResponseDto>.Failure(ErrorType.Validation, input.Error);

        var role = await db.CustomRoles.FirstOrDefaultAsync(r => r.Id == id && r.CompanyId == companyId, cancellationToken);

        if (role is null)
            return Result<RoleResponseDto>.Failure(ErrorType.NotFound, RoleNotFound);

        if (await NameExistsAsync(companyId, input.Name, exceptId: id, cancellationToken))
            return Result<RoleResponseDto>.Failure(ErrorType.Conflict, NameTaken);

        role.Name = input.Name;
        role.Description = input.Description;
        role.Permissions = input.Permissions;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<RoleResponseDto>.Failure(ErrorType.Conflict, NameTaken);
        }

        var userCount = await db.Users.CountAsync(u => u.CustomRoleId == id, cancellationToken);
        return new RoleResponseDto(ToDto(role, userCount));
    }

    public async Task<Result<SuccessResponse>> DeleteAsync(string companyId, string id, CancellationToken cancellationToken)
    {
        var role = await db.CustomRoles.FirstOrDefaultAsync(r => r.Id == id && r.CompanyId == companyId, cancellationToken);

        if (role is null)
            return Result<SuccessResponse>.Failure(ErrorType.NotFound, RoleNotFound);

        if (await db.Users.AnyAsync(u => u.CustomRoleId == id, cancellationToken))
            return Result<SuccessResponse>.Failure(ErrorType.Conflict, RoleInUse);

        db.CustomRoles.Remove(role);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Given to a user since the check above: the restricted foreign key refused the delete.
            return Result<SuccessResponse>.Failure(ErrorType.Conflict, RoleInUse);
        }

        return new SuccessResponse();
    }

    /// <summary>Gives a company user one of the company's custom roles, or takes it away when RoleId is empty.
    /// The user's access level (admin, itadmin, staff) does not change.</summary>
    public async Task<Result<CompanyUserResponseDto>> AssignAsync(
        string companyId, string userId, AssignRoleRequestDto request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.CompanyId == companyId, cancellationToken);

        if (user is null)
            return Result<CompanyUserResponseDto>.Failure(ErrorType.NotFound, "İstifadəçi tapılmadı.");

        var roleId = string.IsNullOrWhiteSpace(request.RoleId) ? null : request.RoleId.Trim();
        string? roleName = null;

        if (roleId is not null)
        {
            roleName = await db.CustomRoles
                .Where(r => r.Id == roleId && r.CompanyId == companyId)
                .Select(r => r.Name)
                .FirstOrDefaultAsync(cancellationToken);

            if (roleName is null)
                return Result<CompanyUserResponseDto>.Failure(ErrorType.NotFound, RoleNotFound);
        }

        if (user.CustomRoleId != roleId)
        {
            user.CustomRoleId = roleId;

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // The role was deleted between the lookup above and the save.
                return Result<CompanyUserResponseDto>.Failure(ErrorType.NotFound, RoleNotFound);
            }
        }

        return new CompanyUserResponseDto(CompanyUserDto.From(user, roleName));
    }

    // Compared in memory with Azerbaijani casing: the database collation does not pair İ/i, so "MALİYYƏÇİ" and
    // "Maliyyəçi" would both pass its unique index. A company has few roles, so loading the names is cheap.
    private async Task<bool> NameExistsAsync(string companyId, string name, string? exceptId, CancellationToken cancellationToken)
    {
        var names = await db.CustomRoles
            .Where(r => r.CompanyId == companyId && r.Id != exceptId)
            .Select(r => r.Name)
            .ToListAsync(cancellationToken);

        return names.Contains(name, NameComparer);
    }

    private static RoleDto ToDto(CustomRole role, int userCount) =>
        new(role.Id, role.Name, role.Description, role.Permissions, userCount, role.CreatedAt);

    private sealed record RoleInput(string Name, string? Description, List<string> Permissions, string? Error);

    private static RoleInput Normalize(string? rawName, string? rawDescription, IReadOnlyList<string>? rawPermissions)
    {
        var name = rawName?.Trim() ?? string.Empty;
        var description = string.IsNullOrWhiteSpace(rawDescription) ? null : rawDescription.Trim();
        var permissions = (rawPermissions ?? [])
            .Select(p => p?.Trim().ToLowerInvariant() ?? string.Empty)
            .Distinct()
            .ToList();

        string? error = null;

        if (name.Length == 0)
            error = "Rolun adı tələb olunur.";
        else if (name.Length > NameMaxLength)
            error = $"Rolun adı {NameMaxLength} simvoldan uzun ola bilməz.";
        else if (description?.Length > DescriptionMaxLength)
            error = $"Təsvir {DescriptionMaxLength} simvoldan uzun ola bilməz.";
        else if (permissions.FirstOrDefault(p => !AppPermissions.All.Contains(p)) is { } unknown)
            error = $"Naməlum icazə: \"{unknown}\".";

        // Kept in the canonical module order, so the stored list does not depend on the client's order.
        var ordered = AppPermissions.All.Where(permissions.Contains).ToList();

        return new RoleInput(name, description, ordered, error);
    }
}

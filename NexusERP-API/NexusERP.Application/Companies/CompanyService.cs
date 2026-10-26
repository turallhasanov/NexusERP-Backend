using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Companies;

public class CompanyService(IApplicationDbContext db, IJwtTokenGenerator jwtTokenGenerator) : ICompanyService
{
    // Must match the column lengths in CompanyConfiguration.
    internal const int NameMaxLength = 200;
    internal const int VoenMaxLength = 50;

    /// <summary>Role of the user who creates a company.</summary>
    public const string FounderRole = AppRoles.ITAdmin;
    public const string StaffRole = AppRoles.Staff;

    private const string AlreadyInCompany = "Sizin artıq şirkətiniz var.";

    // Creating or joining would turn the platform account into a company user.
    private const string PlatformAdminCannotJoin = "Platforma administratoru şirkətə daxil ola bilməz.";

    public async Task<Result<CurrentCompanyResponseDto>> GetForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([userId], cancellationToken);

        if (user is null)
            return Result<CurrentCompanyResponseDto>.Failure(ErrorType.NotFound, "İstifadəçi tapılmadı.");

        if (user.CompanyId is null)
            return new CurrentCompanyResponseDto(null);

        var company = await db.Companies.FindAsync([user.CompanyId], cancellationToken);

        return new CurrentCompanyResponseDto(company is null ? null : CompanyDto.From(company));
    }

    /// <summary>Creates a company with a fresh invite code; the creator becomes its IT admin.</summary>
    public async Task<Result<CompanyResponseDto>> CreateAsync(string userId, CreateCompanyRequestDto request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        var voen = string.IsNullOrWhiteSpace(request.Voen) ? null : request.Voen.Trim();

        if (ValidateProfile(name, voen) is { } profileError)
            return Result<CompanyResponseDto>.Failure(ErrorType.Validation, profileError);

        var user = await db.Users.FindAsync([userId], cancellationToken);

        if (user is null)
            return Result<CompanyResponseDto>.Failure(ErrorType.NotFound, "İstifadəçi tapılmadı.");

        if (user.Role == AppRoles.SuperAdmin)
            return Result<CompanyResponseDto>.Failure(ErrorType.Forbidden, PlatformAdminCannotJoin);

        if (user.CompanyId is not null)
            return Result<CompanyResponseDto>.Failure(ErrorType.Conflict, AlreadyInCompany);

        var company = new Company { Name = name!, Voen = voen, InviteCode = await NewInviteCodeAsync(db, cancellationToken) };

        // The company and the user's assignment commit together; disposing without Commit rolls back.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        db.Companies.Add(company);
        await db.SaveChangesAsync(cancellationToken);

        // Compare-and-set, as in JoinAsync: a join (or another create) that landed since the check above wins,
        // and this company is rolled back instead of silently moving the user.
        var assigned = await db.Users
            .Where(u => u.Id == userId && u.CompanyId == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.CompanyId, company.Id)
                .SetProperty(u => u.Role, FounderRole), cancellationToken);

        if (assigned == 0)
            return Result<CompanyResponseDto>.Failure(ErrorType.Conflict, AlreadyInCompany);

        await transaction.CommitAsync(cancellationToken);

        // Mirror the stored values for the new token; nothing is saved through the tracked entity.
        user.CompanyId = company.Id;
        user.Role = FounderRole;

        return ToResponse(company, user);
    }

    /// <summary>Puts a user without a company into the company with this invite code, as staff.</summary>
    public async Task<Result<CompanyResponseDto>> JoinAsync(string userId, JoinCompanyRequestDto request, CancellationToken cancellationToken)
    {
        var code = InviteCode.Normalize(request.InviteCode ?? string.Empty);

        if (code.Length == 0)
            return Result<CompanyResponseDto>.Failure(ErrorType.Validation, "Dəvət kodu tələb olunur.");

        if (code.Length != InviteCode.Length)
            return Result<CompanyResponseDto>.Failure(ErrorType.Validation, $"Dəvət kodu {InviteCode.Length} simvoldan ibarət olmalıdır.");

        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.InviteCode == code, cancellationToken);

        if (company is null)
            return Result<CompanyResponseDto>.Failure(ErrorType.NotFound, "Bu dəvət kodu ilə şirkət tapılmadı.");

        if (company.Status != CompanyStatus.Active)
            return Result<CompanyResponseDto>.Failure(ErrorType.Forbidden, "Bu şirkət hazırda aktiv deyil. Ona qoşulmaq mümkün deyil.");

        var user = await db.Users.FindAsync([userId], cancellationToken);

        if (user is null)
            return Result<CompanyResponseDto>.Failure(ErrorType.NotFound, "İstifadəçi tapılmadı.");

        if (user.Role == AppRoles.SuperAdmin)
            return Result<CompanyResponseDto>.Failure(ErrorType.Forbidden, PlatformAdminCannotJoin);

        if (user.CompanyId is not null)
            return Result<CompanyResponseDto>.Failure(ErrorType.Conflict, AlreadyInCompany);

        // Compare-and-set: two joins (or a join and a create) at once cannot both assign a company.
        var joined = await db.Users
            .Where(u => u.Id == userId && u.CompanyId == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.CompanyId, company.Id)
                .SetProperty(u => u.Role, StaffRole), cancellationToken);

        if (joined == 0)
            return Result<CompanyResponseDto>.Failure(ErrorType.Conflict, AlreadyInCompany);

        // Mirror the stored values for the new token; nothing is saved through the tracked entity.
        user.CompanyId = company.Id;
        user.Role = StaffRole;

        return ToResponse(company, user);
    }

    public async Task<Result<MyCompanyResponseDto>> GetMyCompanyAsync(string companyId, CancellationToken cancellationToken)
    {
        var company = await db.Companies
            .AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => new MyCompanyDto(
                c.Id,
                c.Name,
                c.Voen,
                c.InviteCode,
                db.Users.Count(u => u.CompanyId == c.Id)))
            .FirstOrDefaultAsync(cancellationToken);

        return company is null
            ? Result<MyCompanyResponseDto>.Failure(ErrorType.NotFound, "Şirkət tapılmadı.")
            : new MyCompanyResponseDto(company);
    }

    public async Task<Result<ItemsResponseDto<CompanyUserDto>>> GetUsersAsync(string companyId, CancellationToken cancellationToken)
    {
        var users = await db.Users
            .AsNoTracking()
            .Where(u => u.CompanyId == companyId)
            .OrderBy(u => u.CreatedAt)
            .Select(u => new CompanyUserDto(
                u.Id,
                u.Name,
                u.Email,
                u.Role,
                u.CustomRoleId,
                db.CustomRoles.Where(r => r.Id == u.CustomRoleId).Select(r => r.Name).FirstOrDefault(),
                u.IsActive,
                u.CreatedAt))
            .ToListAsync(cancellationToken);

        return new ItemsResponseDto<CompanyUserDto>(users);
    }

    /// <summary>
    /// Gives a user of the company the role admin, itadmin or staff. The new role is in the user's token from
    /// their next sign-in (or company switch); until then the old token keeps the old role.
    /// </summary>
    public async Task<Result<CompanyUserResponseDto>> UpdateUserRoleAsync(
        string currentUserId, string companyId, string userId, UpdateCompanyUserRoleRequestDto request, CancellationToken cancellationToken)
    {
        var role = request.Role?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(role) || !AppRoles.CompanyRoles.Contains(role))
            return Result<CompanyUserResponseDto>.Failure(ErrorType.Validation, "Rol \"admin\", \"itadmin\" və ya \"staff\" olmalıdır.");

        // The caller is a manager, so the company always keeps at least one: they cannot demote themselves.
        if (userId == currentUserId)
            return Result<CompanyUserResponseDto>.Failure(ErrorType.Forbidden, "Öz rolunuzu dəyişə bilməzsiniz.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.CompanyId == companyId, cancellationToken);

        if (user is null)
            return Result<CompanyUserResponseDto>.Failure(ErrorType.NotFound, "İstifadəçi tapılmadı.");

        if (user.Role != role)
        {
            user.Role = role;
            await db.SaveChangesAsync(cancellationToken);
        }

        var customRoleName = user.CustomRoleId is null
            ? null
            : await db.CustomRoles.Where(r => r.Id == user.CustomRoleId).Select(r => r.Name).FirstOrDefaultAsync(cancellationToken);

        return new CompanyUserResponseDto(CompanyUserDto.From(user, customRoleName));
    }

    public Task<CompanyStatus?> GetStatusAsync(string companyId, CancellationToken cancellationToken) =>
        db.Companies
            .AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => (CompanyStatus?)c.Status)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Checks an already trimmed name and VÖEN (null when empty); shared with platform admin tenant creation.</summary>
    internal static string? ValidateProfile(string? name, string? voen)
    {
        if (string.IsNullOrEmpty(name))
            return "Şirkət adı tələb olunur.";

        if (name.Length > NameMaxLength)
            return $"Şirkət adı {NameMaxLength} simvoldan uzun ola bilməz.";

        if (voen?.Length > VoenMaxLength)
            return $"VÖEN {VoenMaxLength} simvoldan uzun ola bilməz.";

        return null;
    }

    // A collision is about one in 10^12 per company; the unique index still guards the race between check and insert.
    internal static async Task<string> NewInviteCodeAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        while (true)
        {
            var code = InviteCode.Generate();

            if (!await db.Companies.AnyAsync(c => c.InviteCode == code, cancellationToken))
                return code;
        }
    }

    private CompanyResponseDto ToResponse(Company company, User user) =>
        new(company.Id, company.Name, company.Voen, company.InviteCode, user.Role, jwtTokenGenerator.GenerateToken(user));
}

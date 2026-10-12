using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Companies;

public class CompanyService(IApplicationDbContext db, IJwtTokenGenerator jwtTokenGenerator) : ICompanyService
{
    // Must match the column lengths in CompanyConfiguration.
    private const int NameMaxLength = 200;
    private const int VoenMaxLength = 50;

    public const string AdminRole = "admin";
    public const string StaffRole = "staff";

    private const string AlreadyInCompany = "Sizin artıq şirkətiniz var.";

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

    /// <summary>Creates a company with a fresh invite code; the creator becomes its admin.</summary>
    public async Task<Result<CompanyResponseDto>> CreateAsync(string userId, CreateCompanyRequestDto request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        var voen = string.IsNullOrWhiteSpace(request.Voen) ? null : request.Voen.Trim();

        if (string.IsNullOrEmpty(name))
            return Result<CompanyResponseDto>.Failure(ErrorType.Validation, "Şirkət adı tələb olunur.");

        if (name.Length > NameMaxLength)
            return Result<CompanyResponseDto>.Failure(ErrorType.Validation, $"Şirkət adı {NameMaxLength} simvoldan uzun ola bilməz.");

        if (voen?.Length > VoenMaxLength)
            return Result<CompanyResponseDto>.Failure(ErrorType.Validation, $"VÖEN {VoenMaxLength} simvoldan uzun ola bilməz.");

        var user = await db.Users.FindAsync([userId], cancellationToken);

        if (user is null)
            return Result<CompanyResponseDto>.Failure(ErrorType.NotFound, "İstifadəçi tapılmadı.");

        if (user.CompanyId is not null)
            return Result<CompanyResponseDto>.Failure(ErrorType.Conflict, AlreadyInCompany);

        var company = new Company { Name = name, Voen = voen, InviteCode = await NewInviteCodeAsync(cancellationToken) };

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
                .SetProperty(u => u.Role, AdminRole), cancellationToken);

        if (assigned == 0)
            return Result<CompanyResponseDto>.Failure(ErrorType.Conflict, AlreadyInCompany);

        await transaction.CommitAsync(cancellationToken);

        // Mirror the stored values for the new token; nothing is saved through the tracked entity.
        user.CompanyId = company.Id;
        user.Role = AdminRole;

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

        var user = await db.Users.FindAsync([userId], cancellationToken);

        if (user is null)
            return Result<CompanyResponseDto>.Failure(ErrorType.NotFound, "İstifadəçi tapılmadı.");

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

    private CompanyResponseDto ToResponse(Company company, User user) =>
        new(company.Id, company.Name, company.Voen, company.InviteCode, user.Role, jwtTokenGenerator.GenerateToken(user));

    // A collision is about one in 10^12 per company; the unique index still guards the race between check and insert.
    private async Task<string> NewInviteCodeAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var code = InviteCode.Generate();

            if (!await db.Companies.AnyAsync(c => c.InviteCode == code, cancellationToken))
                return code;
        }
    }
}

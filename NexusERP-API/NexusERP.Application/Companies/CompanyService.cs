using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Companies;

public class CompanyService(IApplicationDbContext db, IJwtTokenGenerator jwtTokenGenerator) : ICompanyService
{
    // Must match the column lengths in CompanyConfiguration.
    private const int NameMaxLength = 200;
    private const int VoenMaxLength = 50;

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
            return Result<CompanyResponseDto>.Failure(ErrorType.Conflict, "Sizin artıq şirkətiniz var.");

        var company = new Company { Name = name, Voen = voen };

        db.Companies.Add(company);
        user.CompanyId = company.Id;
        await db.SaveChangesAsync(cancellationToken);

        return new CompanyResponseDto(company.Id, company.Name, company.Voen, jwtTokenGenerator.GenerateToken(user));
    }
}

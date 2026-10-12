using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Units;

public class UnitService(IApplicationDbContext db) : IUnitService
{
    // Must match the column lengths in UnitConfiguration.
    private const int NameMaxLength = 100;
    private const int ShortNameMaxLength = 20;

    public async Task<Result<ItemsResponseDto<UnitDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken)
    {
        var items = await db.Units
            .AsNoTracking()
            .Where(u => u.CompanyId == companyId)
            .OrderBy(u => u.Name)
            .Select(u => new UnitDto(u.Id, u.Name, u.ShortName))
            .ToListAsync(cancellationToken);

        return new ItemsResponseDto<UnitDto>(items);
    }

    public async Task<Result<UnitResponseDto>> CreateAsync(string companyId, CreateUnitRequestDto request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        var shortName = request.ShortName?.Trim();

        if (string.IsNullOrEmpty(name))
            return Result<UnitResponseDto>.Failure(ErrorType.Validation, "Ölçü vahidinin adı tələb olunur.");

        if (name.Length > NameMaxLength)
            return Result<UnitResponseDto>.Failure(ErrorType.Validation, $"Ölçü vahidinin adı {NameMaxLength} simvoldan uzun ola bilməz.");

        if (string.IsNullOrEmpty(shortName))
            return Result<UnitResponseDto>.Failure(ErrorType.Validation, "Ölçü vahidinin qısa adı tələb olunur.");

        if (shortName.Length > ShortNameMaxLength)
            return Result<UnitResponseDto>.Failure(ErrorType.Validation, $"Qısa ad {ShortNameMaxLength} simvoldan uzun ola bilməz.");

        var unit = new Unit { CompanyId = companyId, Name = name, ShortName = shortName };

        db.Units.Add(unit);
        await db.SaveChangesAsync(cancellationToken);

        return UnitResponseDto.From(unit);
    }
}

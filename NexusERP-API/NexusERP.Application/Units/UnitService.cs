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

    private const string UnitNotFound = "Ölçü vahidi tapılmadı.";
    private const string DuplicateName = "Bu ölçü vahidi artıq mövcuddur.";

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

        if (Validate(name, shortName) is { } error)
            return Result<UnitResponseDto>.Failure(ErrorType.Validation, error);

        if (await NameTakenAsync(companyId, name!, exceptId: null, cancellationToken))
            return Result<UnitResponseDto>.Failure(ErrorType.Conflict, DuplicateName);

        var unit = new Unit { CompanyId = companyId, Name = name!, ShortName = shortName! };

        db.Units.Add(unit);

        if (!await TrySaveAsync(companyId, name!, null, cancellationToken))
            return Result<UnitResponseDto>.Failure(ErrorType.Conflict, DuplicateName);

        return UnitResponseDto.From(unit);
    }

    public async Task<Result<UnitResponseDto>> UpdateAsync(string companyId, string id, UpdateUnitRequestDto request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        var shortName = request.ShortName?.Trim();

        if (Validate(name, shortName) is { } error)
            return Result<UnitResponseDto>.Failure(ErrorType.Validation, error);

        var unit = await db.Units.FirstOrDefaultAsync(u => u.Id == id && u.CompanyId == companyId, cancellationToken);

        if (unit is null)
            return Result<UnitResponseDto>.Failure(ErrorType.NotFound, UnitNotFound);

        if (await NameTakenAsync(companyId, name!, exceptId: id, cancellationToken))
            return Result<UnitResponseDto>.Failure(ErrorType.Conflict, DuplicateName);

        unit.Name = name!;
        unit.ShortName = shortName!;

        if (!await TrySaveAsync(companyId, name!, id, cancellationToken))
            return Result<UnitResponseDto>.Failure(ErrorType.Conflict, DuplicateName);

        return UnitResponseDto.From(unit);
    }

    // The column collation is case-insensitive (SQL_Latin1_General_CP1_CI_AS), so "Ədəd" matches "ədəd" here and
    // in the unique (CompanyId, Name) index.
    private Task<bool> NameTakenAsync(string companyId, string name, string? exceptId, CancellationToken cancellationToken) =>
        db.Units.AnyAsync(u => u.CompanyId == companyId && u.Name == name && u.Id != exceptId, cancellationToken);

    // Two requests can both pass NameTakenAsync; the unique index lets only one through.
    private async Task<bool> TrySaveAsync(string companyId, string name, string? exceptId, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            if (await NameTakenAsync(companyId, name, exceptId, cancellationToken))
                return false;

            throw;
        }
    }

    private static string? Validate(string? name, string? shortName)
    {
        if (string.IsNullOrEmpty(name))
            return "Ölçü vahidinin adı tələb olunur.";

        if (name.Length > NameMaxLength)
            return $"Ölçü vahidinin adı {NameMaxLength} simvoldan uzun ola bilməz.";

        if (string.IsNullOrEmpty(shortName))
            return "Ölçü vahidinin qısa adı tələb olunur.";

        if (shortName.Length > ShortNameMaxLength)
            return $"Qısa ad {ShortNameMaxLength} simvoldan uzun ola bilməz.";

        return null;
    }
}

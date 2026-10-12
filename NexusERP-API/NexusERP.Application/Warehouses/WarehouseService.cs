using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Warehouses;

public class WarehouseService(IApplicationDbContext db) : IWarehouseService
{
    private const string CodePrefix = "DPO";

    // Must match the column lengths in WarehouseConfiguration.
    private const int NameMaxLength = 200;
    private const int AddressMaxLength = 500;

    public async Task<Result<ItemsResponseDto<WarehouseDto>>> GetActiveAsync(string companyId, CancellationToken cancellationToken)
    {
        var items = await db.Warehouses
            .AsNoTracking()
            .Where(w => w.CompanyId == companyId && w.IsActive)
            .OrderBy(w => w.Code.Length).ThenBy(w => w.Code)
            .Select(w => new WarehouseDto(w.Id, w.Code, w.Name, w.Address, w.IsActive))
            .ToListAsync(cancellationToken);

        return new ItemsResponseDto<WarehouseDto>(items);
    }

    public async Task<Result<WarehouseResponseDto>> CreateAsync(string companyId, CreateWarehouseRequestDto request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        var address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();

        if (string.IsNullOrEmpty(name))
            return Result<WarehouseResponseDto>.Failure(ErrorType.Validation, "Anbar adı tələb olunur.");

        if (name.Length > NameMaxLength)
            return Result<WarehouseResponseDto>.Failure(ErrorType.Validation, $"Anbar adı {NameMaxLength} simvoldan uzun ola bilməz.");

        if (address?.Length > AddressMaxLength)
            return Result<WarehouseResponseDto>.Failure(ErrorType.Validation, $"Ünvan {AddressMaxLength} simvoldan uzun ola bilməz.");

        var warehouse = new Warehouse { CompanyId = companyId, Name = name, Address = address };

        db.Warehouses.Add(warehouse);
        await SequentialCode.AssignAndSaveAsync(
            db,
            db.Warehouses.Where(w => w.CompanyId == companyId).Select(w => w.Code),
            CodePrefix,
            code => warehouse.Code = code,
            cancellationToken);

        return WarehouseResponseDto.From(warehouse);
    }
}

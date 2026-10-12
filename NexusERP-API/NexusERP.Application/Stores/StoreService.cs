using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Stores;

public class StoreService(IApplicationDbContext db) : IStoreService
{
    private const string CodePrefix = "MGZ";

    // Must match the column lengths in StoreConfiguration.
    private const int NameMaxLength = 200;
    private const int AddressMaxLength = 500;

    public async Task<Result<ItemsResponseDto<StoreDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken)
    {
        var items = await db.Stores
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId)
            .OrderBy(s => s.Code.Length).ThenBy(s => s.Code)
            .Select(s => new StoreDto(
                s.Id,
                s.Code,
                s.Name,
                s.Address,
                s.IsActive,
                s.WarehouseId,
                new StoreWarehouseDto(s.Warehouse!.Id, s.Warehouse.Code, s.Warehouse.Name)))
            .ToListAsync(cancellationToken);

        return new ItemsResponseDto<StoreDto>(items);
    }

    public async Task<Result<StoreResponseDto>> CreateAsync(string companyId, CreateStoreRequestDto request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        var warehouseId = request.WarehouseId?.Trim();
        var address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();

        if (string.IsNullOrEmpty(name))
            return Result<StoreResponseDto>.Failure(ErrorType.Validation, "Mağaza adı tələb olunur.");

        if (name.Length > NameMaxLength)
            return Result<StoreResponseDto>.Failure(ErrorType.Validation, $"Mağaza adı {NameMaxLength} simvoldan uzun ola bilməz.");

        if (string.IsNullOrEmpty(warehouseId))
            return Result<StoreResponseDto>.Failure(ErrorType.Validation, "Anbar seçilməlidir.");

        if (address?.Length > AddressMaxLength)
            return Result<StoreResponseDto>.Failure(ErrorType.Validation, $"Ünvan {AddressMaxLength} simvoldan uzun ola bilməz.");

        // Scoped to the caller's company, so another company's warehouse looks exactly like a missing one.
        var warehouse = await db.Warehouses
            .FirstOrDefaultAsync(w => w.Id == warehouseId && w.CompanyId == companyId, cancellationToken);

        if (warehouse is null)
            return Result<StoreResponseDto>.Failure(ErrorType.NotFound, "Anbar tapılmadı.");

        if (!warehouse.IsActive)
            return Result<StoreResponseDto>.Failure(ErrorType.Validation, "Seçilmiş anbar aktiv deyil.");

        var store = new Store { CompanyId = companyId, WarehouseId = warehouse.Id, Name = name, Address = address };

        db.Stores.Add(store);
        await SequentialCode.AssignAndSaveAsync(
            db,
            db.Stores.Where(s => s.CompanyId == companyId).Select(s => s.Code),
            CodePrefix,
            code => store.Code = code,
            cancellationToken);

        return StoreResponseDto.From(store, warehouse);
    }
}

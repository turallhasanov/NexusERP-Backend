using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Warehouses;

public class WarehouseService(IApplicationDbContext db) : IWarehouseService
{
    private const string CodePrefix = "DPO";

    // Must match the column lengths in WarehouseConfiguration.
    private const int NameMaxLength = 200;
    private const int AddressMaxLength = 500;
    private const int ResponsiblePersonMaxLength = 200;

    // decimal(18, 3): at most 15 digits before the point.
    private const decimal CapacityMax = 999_999_999_999_999m;

    // Newest stock movements shown on the warehouse's details.
    private const int MovementLimit = 50;

    private const string WarehouseNotFound = "Anbar tapılmadı.";

    public async Task<Result<ItemsResponseDto<WarehouseDto>>> GetActiveAsync(string companyId, CancellationToken cancellationToken)
    {
        var items = await db.Warehouses
            .AsNoTracking()
            .Where(w => w.CompanyId == companyId && w.IsActive)
            .OrderBy(w => w.Code.Length).ThenBy(w => w.Code)
            .Select(WarehouseDto.Projection(db.Stocks))
            .ToListAsync(cancellationToken);

        return new ItemsResponseDto<WarehouseDto>(items);
    }

    public async Task<Result<WarehouseDetailsResponseDto>> GetDetailsAsync(string companyId, string id, CancellationToken cancellationToken)
    {
        var warehouse = await FindDtoAsync(companyId, id, cancellationToken);

        if (warehouse is null || !warehouse.IsActive)
            return Result<WarehouseDetailsResponseDto>.Failure(ErrorType.NotFound, WarehouseNotFound);

        var stocks = await db.Stocks
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId && s.WarehouseId == id && s.Quantity != 0)
            .OrderBy(s => s.Product!.Name)
            .Select(s => new WarehouseStockDto(
                s.ProductId,
                new ProductRefDto(s.Product!.Id, s.Product.Code, s.Product.Name, s.Product.Barcode, s.Product.Unit!.ShortName),
                s.Quantity))
            .ToListAsync(cancellationToken);

        // Only approved documents have moved stock; drafts are left out.
        var incoming = await db.Purchases
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId && p.WarehouseId == id && p.Status == PurchaseStatus.Approved)
            .OrderByDescending(p => p.DocumentDate)
            .Take(MovementLimit)
            .Select(p => new WarehouseMovementDto(
                p.Id,
                p.DocumentNumber,
                p.DocumentDate,
                WarehouseMovementType.In,
                p.Customer!.Name,
                p.Items.Sum(i => i.Quantity),
                p.TotalAmount))
            .ToListAsync(cancellationToken);

        var outgoing = await db.Sales
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId && s.WarehouseId == id && s.Status == SaleStatus.Approved)
            .OrderByDescending(s => s.DocumentDate)
            .Take(MovementLimit)
            .Select(s => new WarehouseMovementDto(
                s.Id,
                s.DocumentNumber,
                s.DocumentDate,
                WarehouseMovementType.Out,
                s.Customer != null ? s.Customer.Name : null,
                s.Items.Sum(i => i.Quantity),
                s.TotalAmount))
            .ToListAsync(cancellationToken);

        var movements = incoming
            .Concat(outgoing)
            .OrderByDescending(m => m.DocumentDate)
            .Take(MovementLimit)
            .ToList();

        return new WarehouseDetailsResponseDto(warehouse, stocks, movements);
    }

    public async Task<Result<WarehouseResponseDto>> CreateAsync(string companyId, CreateWarehouseRequestDto request, CancellationToken cancellationToken)
    {
        var fields = Normalize(request.Name, request.Address, request.ResponsiblePerson, request.Capacity);

        if (fields.Error is not null)
            return Result<WarehouseResponseDto>.Failure(ErrorType.Validation, fields.Error);

        var warehouse = new Warehouse { CompanyId = companyId };
        fields.ApplyTo(warehouse);

        db.Warehouses.Add(warehouse);
        await SequentialCode.AssignAndSaveAsync(
            db,
            db.Warehouses.Where(w => w.CompanyId == companyId).Select(w => w.Code),
            CodePrefix,
            code => warehouse.Code = code,
            cancellationToken);

        // A new warehouse holds no stock yet.
        return WarehouseResponseDto.From(new WarehouseDto(
            warehouse.Id, warehouse.Code, warehouse.Name, warehouse.Address, warehouse.ResponsiblePerson,
            warehouse.Capacity, warehouse.IsActive, 0, 0));
    }

    public async Task<Result<WarehouseResponseDto>> UpdateAsync(string companyId, string id, UpdateWarehouseRequestDto request, CancellationToken cancellationToken)
    {
        var fields = Normalize(request.Name, request.Address, request.ResponsiblePerson, request.Capacity);

        if (fields.Error is not null)
            return Result<WarehouseResponseDto>.Failure(ErrorType.Validation, fields.Error);

        var warehouse = await db.Warehouses.FirstOrDefaultAsync(
            w => w.Id == id && w.CompanyId == companyId && w.IsActive, cancellationToken);

        if (warehouse is null)
            return Result<WarehouseResponseDto>.Failure(ErrorType.NotFound, WarehouseNotFound);

        fields.ApplyTo(warehouse);
        await db.SaveChangesAsync(cancellationToken);

        var dto = await FindDtoAsync(companyId, id, cancellationToken);
        return WarehouseResponseDto.From(dto!);
    }

    /// <summary>
    /// Refused (409) while the warehouse holds stock, supplies an active store or has draft documents.
    /// A warehouse that past documents still reference is deactivated instead of removed, so they keep it.
    /// </summary>
    public async Task<Result<SuccessResponse>> DeleteAsync(string companyId, string id, CancellationToken cancellationToken)
    {
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(
            w => w.Id == id && w.CompanyId == companyId && w.IsActive, cancellationToken);

        if (warehouse is null)
            return Result<SuccessResponse>.Failure(ErrorType.NotFound, WarehouseNotFound);

        if (await db.Stocks.AnyAsync(s => s.WarehouseId == id && s.Quantity != 0, cancellationToken))
            return Result<SuccessResponse>.Failure(ErrorType.Conflict, "Anbarda məhsul var, silinə bilməz.");

        if (await db.Stores.AnyAsync(s => s.WarehouseId == id && s.IsActive, cancellationToken))
            return Result<SuccessResponse>.Failure(ErrorType.Conflict, "Bu anbara bağlı mağaza var, silinə bilməz.");

        var hasDrafts =
            await db.Purchases.AnyAsync(p => p.WarehouseId == id && p.Status == PurchaseStatus.Draft, cancellationToken) ||
            await db.Sales.AnyAsync(s => s.WarehouseId == id && s.Status == SaleStatus.Draft, cancellationToken);

        if (hasDrafts)
            return Result<SuccessResponse>.Failure(ErrorType.Conflict, "Bu anbarda təsdiqlənməmiş sənəd var, silinə bilməz.");

        // Stocks, stores, purchases and sales keep restricted foreign keys to the warehouse.
        var isReferenced =
            await db.Stocks.AnyAsync(s => s.WarehouseId == id, cancellationToken) ||
            await db.Stores.AnyAsync(s => s.WarehouseId == id, cancellationToken) ||
            await db.Purchases.AnyAsync(p => p.WarehouseId == id, cancellationToken) ||
            await db.Sales.AnyAsync(s => s.WarehouseId == id, cancellationToken);

        if (isReferenced)
            warehouse.IsActive = false;
        else
            db.Warehouses.Remove(warehouse);

        await db.SaveChangesAsync(cancellationToken);

        return new SuccessResponse();
    }

    private Task<WarehouseDto?> FindDtoAsync(string companyId, string id, CancellationToken cancellationToken) =>
        db.Warehouses
            .AsNoTracking()
            .Where(w => w.Id == id && w.CompanyId == companyId)
            .Select(WarehouseDto.Projection(db.Stocks))
            .FirstOrDefaultAsync(cancellationToken);

    private static WarehouseFields Normalize(string? name, string? address, string? responsiblePerson, decimal? capacity)
    {
        var fields = new WarehouseFields(
            name?.Trim() ?? string.Empty,
            string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
            string.IsNullOrWhiteSpace(responsiblePerson) ? null : responsiblePerson.Trim(),
            capacity,
            null);

        if (fields.Name.Length == 0)
            return fields with { Error = "Anbar adı tələb olunur." };

        if (fields.Name.Length > NameMaxLength)
            return fields with { Error = $"Anbar adı {NameMaxLength} simvoldan uzun ola bilməz." };

        if (fields.Address?.Length > AddressMaxLength)
            return fields with { Error = $"Ünvan {AddressMaxLength} simvoldan uzun ola bilməz." };

        if (fields.ResponsiblePerson?.Length > ResponsiblePersonMaxLength)
            return fields with { Error = $"Məsul şəxs {ResponsiblePersonMaxLength} simvoldan uzun ola bilməz." };

        if (capacity is <= 0 or > CapacityMax)
            return fields with { Error = "Tutum müsbət ədəd olmalıdır." };

        return fields;
    }

    private sealed record WarehouseFields(string Name, string? Address, string? ResponsiblePerson, decimal? Capacity, string? Error)
    {
        public void ApplyTo(Warehouse warehouse)
        {
            warehouse.Name = Name;
            warehouse.Address = Address;
            warehouse.ResponsiblePerson = ResponsiblePerson;
            warehouse.Capacity = Capacity;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Audit;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Stores;

public class StoreService(IApplicationDbContext db, IAuditLogger audit) : IStoreService
{
    private const string CodePrefix = "MGZ";

    // Must match the column lengths in StoreConfiguration.
    private const int NameMaxLength = 200;
    private const int AddressMaxLength = 500;

    private const string StoreNotFound = "Mağaza tapılmadı.";

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
        var fields = await ValidateAsync(companyId, request.Name, request.WarehouseId, request.Address, currentWarehouseId: null, cancellationToken);

        if (!fields.IsSuccess)
            return Result<StoreResponseDto>.Failure(fields.ErrorType, fields.Error!);

        var (name, warehouse, address) = fields.Value!;
        var store = new Store { CompanyId = companyId, WarehouseId = warehouse.Id, Name = name, Address = address };

        db.Stores.Add(store);
        await SequentialCode.AssignAndSaveAsync(
            db,
            db.Stores.Where(s => s.CompanyId == companyId).Select(s => s.Code),
            CodePrefix,
            code => store.Code = code,
            cancellationToken);

        await audit.LogAsync(companyId, AuditModule.Retail, AuditAction.Create,
            $"'{store.Code} · {store.Name}' mağazası yaradıldı", cancellationToken);

        return StoreResponseDto.From(store, warehouse);
    }

    /// <summary>Renames the store, changes its address or the warehouse it is supplied from (for new sales).</summary>
    public async Task<Result<StoreResponseDto>> UpdateAsync(string companyId, string id, UpdateStoreRequestDto request, CancellationToken cancellationToken)
    {
        var store = await db.Stores.FirstOrDefaultAsync(s => s.Id == id && s.CompanyId == companyId, cancellationToken);

        if (store is null)
            return Result<StoreResponseDto>.Failure(ErrorType.NotFound, StoreNotFound);

        var fields = await ValidateAsync(companyId, request.Name, request.WarehouseId, request.Address, store.WarehouseId, cancellationToken);

        if (!fields.IsSuccess)
            return Result<StoreResponseDto>.Failure(fields.ErrorType, fields.Error!);

        var (name, warehouse, address) = fields.Value!;
        store.Name = name;
        store.Address = address;
        store.WarehouseId = warehouse.Id;
        await db.SaveChangesAsync(cancellationToken);

        await audit.LogAsync(companyId, AuditModule.Retail, AuditAction.Update,
            $"'{store.Code} · {store.Name}' mağazasının məlumatları yeniləndi", cancellationToken);

        return StoreResponseDto.From(store, warehouse);
    }

    /// <summary>
    /// Deletes a store nothing depends on: refused (409) while sales, expenses or employees still working there point
    /// at it. Former employees assigned to it become company-wide staff, so their record does not block the delete.
    /// </summary>
    public async Task<Result<SuccessResponse>> DeleteAsync(string companyId, string id, CancellationToken cancellationToken)
    {
        var store = await db.Stores.FirstOrDefaultAsync(s => s.Id == id && s.CompanyId == companyId, cancellationToken);

        if (store is null)
            return Result<SuccessResponse>.Failure(ErrorType.NotFound, StoreNotFound);

        var sales = await db.Sales.CountAsync(s => s.StoreId == id, cancellationToken);
        var expenses = await db.Expenses.CountAsync(e => e.StoreId == id, cancellationToken);
        var employees = await db.Employees.CountAsync(e => e.StoreId == id && e.Status != EmployeeStatus.Terminated, cancellationToken);

        if (sales + expenses + employees > 0)
        {
            var links = new List<string>();
            if (sales > 0) links.Add($"satış: {sales}");
            if (expenses > 0) links.Add($"məxaric: {expenses}");
            if (employees > 0) links.Add($"aktiv işçi: {employees}");

            return Result<SuccessResponse>.Failure(
                ErrorType.Conflict,
                $"Bu mağazaya bağlı əməliyyatlar var, silmək mümkün deyil ({string.Join(", ", links)}).");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.Employees
            .Where(e => e.CompanyId == companyId && e.StoreId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.StoreId, (string?)null), cancellationToken);

        db.Stores.Remove(store);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await audit.LogAsync(companyId, AuditModule.Retail, AuditAction.Delete,
            $"'{store.Code} · {store.Name}' mağazası silindi", cancellationToken);

        return new SuccessResponse();
    }

    /// <summary>
    /// Trimmed name and address and the company's warehouse. A warehouse closed since may stay on the store that
    /// already uses it (<paramref name="currentWarehouseId"/>), but a store cannot move to a closed one.
    /// </summary>
    private async Task<Result<StoreFields>> ValidateAsync(
        string companyId, string? rawName, string? rawWarehouseId, string? rawAddress, string? currentWarehouseId, CancellationToken cancellationToken)
    {
        var name = rawName?.Trim();
        var warehouseId = rawWarehouseId?.Trim();
        var address = string.IsNullOrWhiteSpace(rawAddress) ? null : rawAddress.Trim();

        if (string.IsNullOrEmpty(name))
            return Result<StoreFields>.Failure(ErrorType.Validation, "Mağaza adı tələb olunur.");

        if (name.Length > NameMaxLength)
            return Result<StoreFields>.Failure(ErrorType.Validation, $"Mağaza adı {NameMaxLength} simvoldan uzun ola bilməz.");

        if (string.IsNullOrEmpty(warehouseId))
            return Result<StoreFields>.Failure(ErrorType.Validation, "Anbar seçilməlidir.");

        if (address?.Length > AddressMaxLength)
            return Result<StoreFields>.Failure(ErrorType.Validation, $"Ünvan {AddressMaxLength} simvoldan uzun ola bilməz.");

        // Scoped to the caller's company, so another company's warehouse looks exactly like a missing one.
        var warehouse = await db.Warehouses
            .FirstOrDefaultAsync(w => w.Id == warehouseId && w.CompanyId == companyId, cancellationToken);

        if (warehouse is null)
            return Result<StoreFields>.Failure(ErrorType.NotFound, "Anbar tapılmadı.");

        if (!warehouse.IsActive && warehouse.Id != currentWarehouseId)
            return Result<StoreFields>.Failure(ErrorType.Validation, "Seçilmiş anbar aktiv deyil.");

        return new StoreFields(name, warehouse, address);
    }

    private sealed record StoreFields(string Name, Warehouse Warehouse, string? Address);
}

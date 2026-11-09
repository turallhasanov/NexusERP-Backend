using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Audit;
using NexusERP.Application.CashRegisters;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Application.Customers;
using NexusERP.Application.Integration;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Sales;

public class SaleService(
    IApplicationDbContext db, IStockWriter stockWriter, IAuditLogger audit, IKhazarOrderClient khazarOrders,
    IStorefrontStockSync storefrontSync) : ISaleService
{
    private const string NumberPrefix = "STŞ";

    // Upper bounds of decimal(18,2) money and decimal(18,3) quantity columns.
    private const decimal MaxMoney = 9_999_999_999_999_999.99m;
    private const decimal MaxQuantity = 999_999_999_999_999.999m;

    // Online orders: the fixed account they are booked to, and column lengths from SaleConfiguration.
    private const string OnlineCustomerKey = "khazar-online";
    private const string OnlineCustomerName = "Khazar Online Müştəri";
    private const int ExternalOrderNumberMaxLength = 50;
    private const int RetailNameMaxLength = 200;
    private const int RetailPhoneMaxLength = 50;
    private const int RetailEmailMaxLength = 200;
    private const int RetailAddressMaxLength = 500;

    public async Task<Result<ItemsResponseDto<SaleSummaryDto>>> GetAllAsync(
        string companyId, string? customerId, string? search, CancellationToken cancellationToken)
    {
        var query = db.Sales
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId);

        var customerFilter = customerId?.Trim();
        if (!string.IsNullOrEmpty(customerFilter))
            query = query.Where(s => s.CustomerId == customerFilter);

        // Case follows the column collation (insensitive by default), as in the product search.
        var text = search?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            // A phone typed the local way ("050 123 45 67", "(50) 123-45-67") must find "+994501234567".
            // Phones match only through this key (3+ digits), so a stray "7" does not list every number containing a 7.
            var phoneKey = PhoneSearchKey(text);
            var matchPhone = phoneKey is not null;
            var phoneText = phoneKey ?? string.Empty;

            query = query.Where(s =>
                s.DocumentNumber.Contains(text)
                || (s.Customer != null && (s.Customer.Name.Contains(text) || s.Customer.Code.Contains(text)
                                           || (matchPhone && s.Customer.Phone != null && s.Customer.Phone.Contains(phoneText))))
                || (s.RetailCustomerName != null && s.RetailCustomerName.Contains(text))
                || (matchPhone && s.RetailCustomerPhone != null && s.RetailCustomerPhone.Contains(phoneText)));
        }

        var items = await query
            .OrderByDescending(s => s.DocumentDate)
            .ThenByDescending(s => s.DocumentNumber.Length).ThenByDescending(s => s.DocumentNumber)
            .Select(s => new SaleSummaryDto(
                s.Id,
                s.DocumentNumber,
                s.DocumentDate,
                s.Status,
                s.TotalAmount,
                s.StoreId,
                new CodeNameDto(s.Store!.Id, s.Store.Code, s.Store.Name),
                s.WarehouseId,
                new CodeNameDto(s.Warehouse!.Id, s.Warehouse.Code, s.Warehouse.Name),
                s.CustomerId,
                s.CustomerId == null ? null : new CodeNameDto(s.Customer!.Id, s.Customer.Code, s.Customer.Name),
                s.Items.Count,
                s.Channel,
                s.ExternalOrderNumber,
                s.RetailCustomerName == null
                    ? null
                    : new RetailCustomerDto(s.RetailCustomerName, s.RetailCustomerPhone, s.RetailCustomerEmail, s.RetailCustomerAddress)))
            .ToListAsync(cancellationToken);

        return new ItemsResponseDto<SaleSummaryDto>(items);
    }

    /// <summary>
    /// For text that is only digits and phone punctuation (at least 3 digits): the digits without a +994 country code or a
    /// leading trunk 0, so it matches the stored international form. Null when the text does not look like a phone.
    /// </summary>
    private static string? PhoneSearchKey(string text)
    {
        if (text.Any(c => !char.IsDigit(c) && " +-()".IndexOf(c) < 0))
            return null;

        var digits = new string(text.Where(char.IsDigit).ToArray());

        if (digits.StartsWith("994", StringComparison.Ordinal) && digits.Length > 3)
            digits = digits[3..];
        else if (digits.StartsWith('0'))
            digits = digits[1..];

        return digits.Length >= 3 ? digits : null;
    }

    public async Task<Result<SaleLookupResponseDto>> GetByIdAsync(string companyId, string id, CancellationToken cancellationToken)
    {
        var sale = await FindAsync(companyId, id, cancellationToken);

        return sale is null
            ? Result<SaleLookupResponseDto>.Failure(ErrorType.NotFound, "Satış sənədi tapılmadı.")
            : new SaleLookupResponseDto(sale);
    }

    public async Task<Result<SaleResponseDto>> CreateAsync(string companyId, CreateSaleRequestDto request, CancellationToken cancellationToken)
    {
        // A draft has not been paid; payment into a register happens only at POS checkout.
        if (!string.IsNullOrWhiteSpace(request.CashRegisterId))
            return Result<SaleResponseDto>.Failure(ErrorType.Validation, "Kassa yalnız POS satışında göstərilir.");

        var built = await BuildSaleAsync(companyId, request, cancellationToken);

        if (!built.IsSuccess)
            return Result<SaleResponseDto>.Failure(built.ErrorType, built.Error!);

        var sale = built.Value!;
        sale.Status = SaleStatus.Draft;

        await AddWithNumberAsync(companyId, sale, cancellationToken);

        await audit.LogAsync(companyId, AuditModule.Retail, AuditAction.Create,
            $"'{sale.DocumentNumber}' satış qaiməsi yaradıldı ({sale.TotalAmount:0.00} ₼)", cancellationToken);

        return SaleResponseDto.From((await FindAsync(companyId, sale.Id, cancellationToken))!);
    }

    public async Task<Result<SaleResponseDto>> ApproveAsync(string companyId, string id, CancellationToken cancellationToken)
    {
        // Status change and stock decreases commit together or not at all; disposing without Commit rolls back.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Compare-and-set on Status: if two approvals race, the second blocks on the row lock, then matches
        // nothing, so stock is never decreased twice for the same document.
        var approved = await db.Sales
            .Where(s => s.Id == id && s.CompanyId == companyId && s.Status == SaleStatus.Draft)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SaleStatus.Approved), cancellationToken);

        if (approved == 0)
        {
            var exists = await db.Sales.AnyAsync(s => s.Id == id && s.CompanyId == companyId, cancellationToken);

            return exists
                ? Result<SaleResponseDto>.Failure(ErrorType.Conflict, "Sənəd artıq təsdiqlənib.")
                : Result<SaleResponseDto>.Failure(ErrorType.NotFound, "Satış sənədi tapılmadı.");
        }

        var warehouseId = await db.Sales
            .Where(s => s.Id == id)
            .Select(s => s.WarehouseId)
            .SingleAsync(cancellationToken);

        var lines = await db.SaleItems
            .Where(i => i.SaleId == id)
            .Select(i => new { i.ProductId, i.Quantity })
            .ToListAsync(cancellationToken);

        var shortage = await DecreaseStockAsync(
            companyId, warehouseId, lines.Select(l => (l.ProductId, l.Quantity)), cancellationToken);

        if (shortage is not null)
            return Result<SaleResponseDto>.Failure(ErrorType.Validation, shortage);

        await transaction.CommitAsync(cancellationToken);

        // An online order already took its units from the store's own stock; only store-side sales need pushing.
        var channel = await db.Sales.Where(s => s.Id == id).Select(s => s.Channel).SingleAsync(cancellationToken);
        if (channel != SaleChannel.Online)
            await storefrontSync.SyncAsync(companyId, lines.Select(l => l.ProductId).Distinct().ToList(), cancellationToken);

        var approvedSale = (await FindAsync(companyId, id, cancellationToken))!;
        await audit.LogAsync(companyId, AuditModule.Retail, AuditAction.Update,
            $"'{approvedSale.DocumentNumber}' satış qaiməsi təsdiqləndi ({approvedSale.TotalAmount:0.00} ₼)", cancellationToken);

        return SaleResponseDto.From(approvedSale);
    }

    public async Task<Result<SaleResponseDto>> PosCheckoutAsync(string companyId, CreateSaleRequestDto request, CancellationToken cancellationToken)
    {
        var built = await BuildSaleAsync(companyId, request, cancellationToken);

        if (!built.IsSuccess)
            return Result<SaleResponseDto>.Failure(built.ErrorType, built.Error!);

        var sale = built.Value!;
        sale.Status = SaleStatus.Approved;

        var cashRegisterId = string.IsNullOrWhiteSpace(request.CashRegisterId) ? null : request.CashRegisterId.Trim();

        if (cashRegisterId is not null)
        {
            // Scoped to the caller's company, so another company's register looks exactly like a missing one.
            var cashRegister = await db.CashRegisters
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == cashRegisterId && c.CompanyId == companyId, cancellationToken);

            if (cashRegister is null)
                return Result<SaleResponseDto>.Failure(ErrorType.NotFound, "Kassa tapılmadı.");

            if (!cashRegister.IsActive)
                return Result<SaleResponseDto>.Failure(ErrorType.Validation, "Seçilmiş kassa aktiv deyil.");
        }

        // Stock decrease, the approved document and the register payment commit together; on a shortage
        // nothing is saved at all.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var shortage = await DecreaseStockAsync(
            companyId, sale.WarehouseId, sale.Items.Select(i => (i.ProductId, i.Quantity)), cancellationToken);

        if (shortage is not null)
            return Result<SaleResponseDto>.Failure(ErrorType.Validation, shortage);

        await AddWithNumberAsync(companyId, sale, cancellationToken);

        // A sale of free items has nothing to pay in; a transaction needs a positive amount.
        if (cashRegisterId is not null && sale.TotalAmount > 0)
        {
            // Stock rows are locked before the register row, the same order every time, so this cannot
            // deadlock with other checkouts.
            if (!await CashRegisterBalance.TryIncreaseAsync(db, companyId, cashRegisterId, sale.TotalAmount, cancellationToken))
                return Result<SaleResponseDto>.Failure(ErrorType.Validation, "Kassa balansı həddindən artıq böyük olar.");

            db.Transactions.Add(new Transaction
            {
                CompanyId = companyId,
                CashRegisterId = cashRegisterId,
                TransactionType = TransactionType.Income,
                Amount = sale.TotalAmount,
                Description = $"POS satışı {sale.DocumentNumber}",
                SaleId = sale.Id,
                TransactionDate = sale.DocumentDate
            });

            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        // After the commit: a Khazar failure is logged inside and cannot undo the sale. A product that reached 0 is
        // pushed as 0, which Khazar shows as sold out for that size/colour.
        await storefrontSync.SyncAsync(companyId, sale.Items.Select(i => i.ProductId).Distinct().ToList(), cancellationToken);

        await audit.LogAsync(companyId, AuditModule.Retail, AuditAction.Create,
            $"'{sale.DocumentNumber}' POS satışı edildi ({sale.TotalAmount:0.00} ₼)", cancellationToken);

        return SaleResponseDto.From((await FindAsync(companyId, sale.Id, cancellationToken))!);
    }

    /// <summary>
    /// A retail order from the online store, saved as a sale and approved right away so stock moves (a shortage
    /// leaves it a draft for staff to resolve). Idempotent on <c>externalOrderNumber</c>: a resent order returns the existing sale.
    /// </summary>
    public async Task<Result<SaleResponseDto>> CreateOnlineOrderAsync(string companyId, CreateOnlineOrderRequestDto request, CancellationToken cancellationToken)
    {
        var externalOrderNumber = request.ExternalOrderNumber?.Trim();
        var customer = request.Customer;
        var name = customer?.FullName?.Trim() is { Length: > 0 } fullName
            ? fullName
            : string.Join(' ', new[] { customer?.FirstName?.Trim(), customer?.LastName?.Trim() }.Where(p => !string.IsNullOrEmpty(p)));
        var phone = string.IsNullOrWhiteSpace(customer?.Phone) ? null : customer.Phone.Trim();
        var email = string.IsNullOrWhiteSpace(customer?.Email) ? null : customer.Email.Trim();
        var address = string.IsNullOrWhiteSpace(customer?.Address) ? null : customer.Address.Trim();

        var error =
            string.IsNullOrEmpty(externalOrderNumber) ? "Sifariş nömrəsi tələb olunur."
            : externalOrderNumber.Length > ExternalOrderNumberMaxLength ? $"Sifariş nömrəsi {ExternalOrderNumberMaxLength} simvoldan uzun ola bilməz."
            : name.Length == 0 ? "Müştərinin adı tələb olunur."
            : name.Length > RetailNameMaxLength ? $"Müştərinin adı {RetailNameMaxLength} simvoldan uzun ola bilməz."
            : phone?.Length > RetailPhoneMaxLength ? $"Telefon {RetailPhoneMaxLength} simvoldan uzun ola bilməz."
            : email?.Length > RetailEmailMaxLength ? $"E-poçt {RetailEmailMaxLength} simvoldan uzun ola bilməz."
            : address?.Length > RetailAddressMaxLength ? $"Ünvan {RetailAddressMaxLength} simvoldan uzun ola bilməz."
            : null;

        if (error is not null)
            return Result<SaleResponseDto>.Failure(ErrorType.Validation, error);

        if (await FindOnlineOrderAsync(companyId, externalOrderNumber!, cancellationToken) is { } existing)
            return SaleResponseDto.From(existing);

        var onlineCustomerId = await GetOnlineCustomerIdAsync(companyId, cancellationToken);

        var built = await BuildSaleAsync(
            companyId,
            new CreateSaleRequestDto(request.StoreId, request.WarehouseId, onlineCustomerId, request.DocumentDate, request.Items),
            cancellationToken);

        if (!built.IsSuccess)
            return Result<SaleResponseDto>.Failure(built.ErrorType, built.Error!);

        var sale = built.Value!;
        sale.Status = SaleStatus.Draft;
        sale.Channel = SaleChannel.Online;
        sale.ExternalOrderNumber = externalOrderNumber;
        sale.RetailCustomerName = name;
        sale.RetailCustomerPhone = phone;
        sale.RetailCustomerEmail = email;
        sale.RetailCustomerAddress = address;

        try
        {
            await AddWithNumberAsync(companyId, sale, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The same order sent twice at once: the unique (company, external number) index lets one through.
            db.Sales.Entry(sale).State = EntityState.Detached;

            if (await FindOnlineOrderAsync(companyId, externalOrderNumber!, cancellationToken) is { } raced)
                return SaleResponseDto.From(raced);

            throw;
        }

        await audit.LogAsync(companyId, AuditModule.Retail, AuditAction.Create,
            $"Khazar online sifarişi {externalOrderNumber} '{sale.DocumentNumber}' satış qaiməsi kimi qəbul edildi ({sale.TotalAmount:0.00} ₼)",
            cancellationToken);

        // The storefront has already taken the customer's order, so stock is deducted now. A shortage rolls the
        // approval back and leaves the draft for staff; the order itself is still accepted.
        var approval = await ApproveAsync(companyId, sale.Id, cancellationToken);

        if (!approval.IsSuccess)
            await audit.LogAsync(companyId, AuditModule.Retail, AuditAction.Update,
                $"Khazar online sifarişi {externalOrderNumber} avtomatik təsdiqlənmədi (qaralama qaldı): {approval.Error}",
                cancellationToken);

        return SaleResponseDto.From((await FindAsync(companyId, sale.Id, cancellationToken))!);
    }

    public Task<Result<SaleResponseDto>> CancelAsync(string companyId, string id, CancellationToken cancellationToken) =>
        CancelMatchingAsync(companyId, s => s.Id == id, "Satış sənədi tapılmadı.", notifyKhazar: true, cancellationToken);

    public async Task<Result<SaleResponseDto>> CancelOnlineOrderAsync(
        string companyId, string externalOrderNumber, CancellationToken cancellationToken)
    {
        var number = externalOrderNumber?.Trim();
        if (string.IsNullOrEmpty(number))
            return Result<SaleResponseDto>.Failure(ErrorType.Validation, "Sifariş nömrəsi tələb olunur.");

        // Khazar asked for this cancellation, so it must not be asked to cancel the same order again.
        return await CancelMatchingAsync(
            companyId,
            s => s.ExternalOrderNumber == number && s.Channel == SaleChannel.Online,
            "Online sifariş tapılmadı.",
            notifyKhazar: false,
            cancellationToken);
    }

    private sealed record CancelTarget(
        string Id, string DocumentNumber, string WarehouseId, SaleStatus Status, SaleChannel Channel, string? ExternalOrderNumber);

    private async Task<Result<SaleResponseDto>> CancelMatchingAsync(
        string companyId,
        System.Linq.Expressions.Expression<Func<Sale, bool>> match,
        string notFoundMessage,
        bool notifyKhazar,
        CancellationToken cancellationToken)
    {
        var sale = await db.Sales
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId)
            .Where(match)
            .Select(s => new CancelTarget(s.Id, s.DocumentNumber, s.WarehouseId, s.Status, s.Channel, s.ExternalOrderNumber))
            .FirstOrDefaultAsync(cancellationToken);

        if (sale is null)
            return Result<SaleResponseDto>.Failure(ErrorType.NotFound, notFoundMessage);

        // A retry after a lost response must not return the stock or the money twice.
        if (sale.Status == SaleStatus.Cancelled)
            return SaleResponseDto.From((await FindAsync(companyId, sale.Id, cancellationToken))!);

        // Khazar first: if it cannot cancel its order (down, or already delivered), nothing changes here either, so the
        // storefront order and this sale never disagree. Its own stock is returned there.
        if (notifyKhazar && sale.Channel == SaleChannel.Online && sale.ExternalOrderNumber is not null)
        {
            var khazar = await khazarOrders.CancelOrderAsync(sale.ExternalOrderNumber, cancellationToken);
            if (!khazar.Success)
                return Result<SaleResponseDto>.Failure(ErrorType.ServiceUnavailable, khazar.Error!);
        }

        // Status change, stock and cash register commit together or not at all; disposing without Commit rolls back.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Compare-and-set on the status that was read: if two cancellations race, only one matches a row,
        // so stock and money are never returned twice.
        var changed = await db.Sales
            .Where(s => s.Id == sale.Id && s.Status == sale.Status)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SaleStatus.Cancelled), cancellationToken);

        if (changed == 0)
            return Result<SaleResponseDto>.Failure(ErrorType.Conflict, "Satış eyni anda dəyişdirildi. Yenidən cəhd edin.");

        var returnedProductIds = new List<string>();

        // A draft never took stock (approval was skipped or rolled back), so only an approved sale gives any back.
        if (sale.Status == SaleStatus.Approved)
        {
            var lines = await db.SaleItems
                .Where(i => i.SaleId == sale.Id)
                .Select(i => new { i.ProductId, i.Quantity })
                .ToListAsync(cancellationToken);

            // Same fixed ordinal order as the decrease, so concurrent documents lock stock rows in one sequence.
            foreach (var line in lines.GroupBy(l => l.ProductId).OrderBy(g => g.Key, StringComparer.Ordinal))
                await stockWriter.IncreaseAsync(companyId, sale.WarehouseId, line.Key, line.Sum(l => l.Quantity), cancellationToken);

            returnedProductIds = lines.Select(l => l.ProductId).Distinct().ToList();
        }

        // A POS sale was paid into a cash register; take that money back out and record the reversal.
        var payments = await db.Transactions
            .Where(t => t.SaleId == sale.Id && t.TransactionType == TransactionType.Income)
            .Select(t => new { t.CashRegisterId, t.Amount })
            .ToListAsync(cancellationToken);

        foreach (var payment in payments.GroupBy(p => p.CashRegisterId))
        {
            var amount = payment.Sum(p => p.Amount);

            if (!await CashRegisterBalance.TryDecreaseAsync(db, companyId, payment.Key, amount, cancellationToken))
                return Result<SaleResponseDto>.Failure(
                    ErrorType.Validation,
                    $"Kassada {amount:0.00} ₼ qalmayıb, '{sale.DocumentNumber}' satışını ləğv etmək mümkün deyil. Əvvəlcə kassaya vəsait əlavə edin.");

            db.Transactions.Add(new Transaction
            {
                CompanyId = companyId,
                CashRegisterId = payment.Key,
                TransactionType = TransactionType.Expense,
                Amount = amount,
                Description = $"POS satışının ləğvi {sale.DocumentNumber}",
                SaleId = sale.Id,
                TransactionDate = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Online orders get their units back inside Khazar itself; store-side sales must be pushed there.
        if (sale.Channel != SaleChannel.Online && returnedProductIds.Count > 0)
            await storefrontSync.SyncAsync(companyId, returnedProductIds, cancellationToken);

        await audit.LogAsync(companyId, AuditModule.Retail, AuditAction.Update,
            sale.ExternalOrderNumber is null
                ? $"'{sale.DocumentNumber}' satış qaiməsi ləğv edildi, məhsullar anbara qaytarıldı"
                : $"Khazar online sifarişi {sale.ExternalOrderNumber} ləğv edildi, '{sale.DocumentNumber}' satış qaiməsi üzrə məhsullar anbara qaytarıldı",
            cancellationToken);

        return SaleResponseDto.From((await FindAsync(companyId, sale.Id, cancellationToken))!);
    }

    private Task<SaleDto?> FindOnlineOrderAsync(string companyId, string externalOrderNumber, CancellationToken cancellationToken) =>
        db.Sales
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId && s.ExternalOrderNumber == externalOrderNumber)
            .Select(SaleDto.Projection)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>The company's "Khazar Online Müştəri" account, created on the first online order.</summary>
    private async Task<string> GetOnlineCustomerIdAsync(string companyId, CancellationToken cancellationToken)
    {
        var existingId = await db.Customers
            .Where(c => c.CompanyId == companyId && c.SystemKey == OnlineCustomerKey)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingId is not null)
            return existingId;

        var customer = new Customer { CompanyId = companyId, Name = OnlineCustomerName, SystemKey = OnlineCustomerKey };
        db.Customers.Add(customer);

        try
        {
            await SequentialCode.AssignAndSaveAsync(
                db,
                db.Customers.Where(c => c.CompanyId == companyId).Select(c => c.Code),
                CustomerService.CodePrefix,
                code => customer.Code = code,
                cancellationToken);

            return customer.Id;
        }
        catch (DbUpdateException)
        {
            // Two first orders at once: the unique (company, system key) index keeps one account.
            db.Customers.Entry(customer).State = EntityState.Detached;

            return await db.Customers
                .Where(c => c.CompanyId == companyId && c.SystemKey == OnlineCustomerKey)
                .Select(c => c.Id)
                .FirstAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Validates the request against the database and builds an unsaved sale with its lines and totals.
    /// Shared by draft creation and POS checkout, which differ only in status and stock handling.
    /// </summary>
    private async Task<Result<Sale>> BuildSaleAsync(string companyId, CreateSaleRequestDto request, CancellationToken cancellationToken)
    {
        var storeId = request.StoreId?.Trim();
        var requestedWarehouseId = string.IsNullOrWhiteSpace(request.WarehouseId) ? null : request.WarehouseId.Trim();
        var customerId = string.IsNullOrWhiteSpace(request.CustomerId) ? null : request.CustomerId.Trim();

        if (string.IsNullOrEmpty(storeId))
            return Result<Sale>.Failure(ErrorType.Validation, "Mağaza seçilməlidir.");

        if (request.Items is not { Count: > 0 } requestItems)
            return Result<Sale>.Failure(ErrorType.Validation, "Sənəddə ən azı bir məhsul olmalıdır.");

        // Validate line shapes before touching the database. Line numbers in messages are 1-based, as the user sees them.
        var lines = new List<(string ProductId, decimal Quantity, decimal? UnitPrice)>(requestItems.Count);

        for (var i = 0; i < requestItems.Count; i++)
        {
            var line = i + 1;
            var item = requestItems[i];
            var productId = item?.ProductId?.Trim();

            if (item is null || string.IsNullOrEmpty(productId))
                return Result<Sale>.Failure(ErrorType.Validation, $"Sətir {line}: məhsul seçilməlidir.");

            if (item.Quantity is not { } rawQuantity)
                return Result<Sale>.Failure(ErrorType.Validation, $"Sətir {line}: miqdar tələb olunur.");

            // Rounded to the column scale first, so what is validated is exactly what gets stored.
            var quantity = Math.Round(rawQuantity, 3, MidpointRounding.AwayFromZero);
            var unitPrice = item.UnitPrice is { } rawUnitPrice ? Math.Round(rawUnitPrice, 2, MidpointRounding.AwayFromZero) : (decimal?)null;

            if (quantity <= 0)
                return Result<Sale>.Failure(ErrorType.Validation, $"Sətir {line}: miqdar sıfırdan böyük olmalıdır.");

            if (quantity > MaxQuantity)
                return Result<Sale>.Failure(ErrorType.Validation, $"Sətir {line}: miqdar həddindən artıq böyükdür.");

            if (unitPrice < 0)
                return Result<Sale>.Failure(ErrorType.Validation, $"Sətir {line}: qiymət mənfi ola bilməz.");

            lines.Add((productId, quantity, unitPrice));
        }

        // Scoped to the caller's company, so another company's records look exactly like missing ones.
        var store = await db.Stores
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == storeId && s.CompanyId == companyId, cancellationToken);

        if (store is null)
            return Result<Sale>.Failure(ErrorType.NotFound, "Mağaza tapılmadı.");

        if (!store.IsActive)
            return Result<Sale>.Failure(ErrorType.Validation, "Seçilmiş mağaza aktiv deyil.");

        var warehouseId = requestedWarehouseId ?? store.WarehouseId;
        var warehouse = await db.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == warehouseId && w.CompanyId == companyId, cancellationToken);

        if (warehouse is null)
            return Result<Sale>.Failure(ErrorType.NotFound, "Anbar tapılmadı.");

        if (!warehouse.IsActive)
            return Result<Sale>.Failure(ErrorType.Validation, "Seçilmiş anbar aktiv deyil.");

        if (customerId is not null)
        {
            var customer = await db.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == customerId && c.CompanyId == companyId, cancellationToken);

            if (customer is null)
                return Result<Sale>.Failure(ErrorType.NotFound, "Kontragent tapılmadı.");

            if (!customer.IsActive)
                return Result<Sale>.Failure(ErrorType.Validation, "Seçilmiş kontragent aktiv deyil.");
        }

        var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId && productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.IsActive, p.SalePrice })
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var sale = new Sale
        {
            CompanyId = companyId,
            StoreId = store.Id,
            WarehouseId = warehouse.Id,
            CustomerId = customerId,
            DocumentDate = request.DocumentDate ?? DateTime.UtcNow
        };

        for (var i = 0; i < lines.Count; i++)
        {
            var line = i + 1;
            var (productId, quantity, requestedUnitPrice) = lines[i];

            if (!products.TryGetValue(productId, out var product))
                return Result<Sale>.Failure(ErrorType.NotFound, $"Sətir {line}: məhsul tapılmadı.");

            if (!product.IsActive)
                return Result<Sale>.Failure(ErrorType.Validation, $"Sətir {line}: məhsul aktiv deyil.");

            var unitPrice = requestedUnitPrice ?? product.SalePrice;

            // Checked by division so the multiplication below can never overflow.
            if (unitPrice > 0 && quantity > MaxMoney / unitPrice)
                return Result<Sale>.Failure(ErrorType.Validation, $"Sətir {line}: məbləğ həddindən artıq böyükdür.");

            sale.Items.Add(new SaleItem
            {
                SaleId = sale.Id,
                LineNumber = line,
                ProductId = productId,
                Quantity = quantity,
                UnitPrice = unitPrice,
                TotalPrice = Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero)
            });
        }

        // Each line fits on its own; with many lines the sum can still exceed the column.
        var totalAmount = sale.Items.Sum(i => i.TotalPrice);

        if (totalAmount > MaxMoney)
            return Result<Sale>.Failure(ErrorType.Validation, "Sənədin ümumi məbləği həddindən artıq böyükdür.");

        sale.TotalAmount = totalAmount;

        return sale;
    }

    private Task AddWithNumberAsync(string companyId, Sale sale, CancellationToken cancellationToken)
    {
        db.Sales.Add(sale);

        return SequentialCode.AssignAndSaveAsync(
            db,
            db.Sales.Where(s => s.CompanyId == companyId).Select(s => s.DocumentNumber),
            NumberPrefix,
            number => sale.DocumentNumber = number,
            cancellationToken);
    }

    /// <summary>
    /// Decreases stock for every product, all or nothing within the caller's transaction. Returns null on
    /// success, or the shortage message for the first product that is not covered (the caller must then
    /// not commit, which undoes any decreases already made).
    /// </summary>
    private async Task<string?> DecreaseStockAsync(
        string companyId,
        string warehouseId,
        IEnumerable<(string ProductId, decimal Quantity)> lines,
        CancellationToken cancellationToken)
    {
        // One decrease per product (a product may span several lines), in a fixed ordinal order shared with
        // purchase approval so concurrent documents lock stock rows in the same sequence and do not deadlock.
        var required = lines
            .GroupBy(l => l.ProductId)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(l => l.Quantity)))
            .OrderBy(r => r.ProductId, StringComparer.Ordinal);

        foreach (var (productId, quantity) in required)
        {
            if (await stockWriter.TryDecreaseAsync(companyId, warehouseId, productId, quantity, cancellationToken))
                continue;

            var available = await db.Stocks
                .Where(s => s.CompanyId == companyId && s.WarehouseId == warehouseId && s.ProductId == productId)
                .Select(s => (decimal?)s.Quantity)
                .FirstOrDefaultAsync(cancellationToken) ?? 0m;

            var productName = await db.Products
                .Where(p => p.Id == productId)
                .Select(p => p.Name)
                .FirstAsync(cancellationToken);

            return $"Anbarda satış üçün kifayət qədər məhsul yoxdur: {productName} " +
                   $"(Qalıq: {FormatQuantity(available)}, Tələb olunan: {FormatQuantity(quantity)})";
        }

        return null;
    }

    private static string FormatQuantity(decimal quantity) => quantity.ToString("0.###", CultureInfo.InvariantCulture);

    private Task<SaleDto?> FindAsync(string companyId, string id, CancellationToken cancellationToken) =>
        db.Sales
            .AsNoTracking()
            .Where(s => s.Id == id && s.CompanyId == companyId)
            .Select(SaleDto.Projection)
            .FirstOrDefaultAsync(cancellationToken);
}

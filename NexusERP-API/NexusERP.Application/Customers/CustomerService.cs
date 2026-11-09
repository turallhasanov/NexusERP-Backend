using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexusERP.Application.Audit;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Customers;

public class CustomerService(IApplicationDbContext db, IAuditLogger audit, ILogger<CustomerService> logger) : ICustomerService
{
    internal const string CodePrefix = "KNT";

    // Must match the column lengths in CustomerConfiguration.
    private const int NameMaxLength = 200;
    private const int VoenMaxLength = 50;
    private const int PhoneMaxLength = 50;
    private const int AddressMaxLength = 500;

    public async Task<Result<ItemsResponseDto<CustomerDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken)
    {
        var items = await db.Customers
            .AsNoTracking()
            .Where(c => c.CompanyId == companyId)
            .OrderBy(c => c.Code.Length).ThenBy(c => c.Code)
            .Select(c => new CustomerDto(c.Id, c.Code, c.Name, c.Voen, c.Phone, c.Address, c.IsActive, c.SystemKey != null))
            .ToListAsync(cancellationToken);

        return new ItemsResponseDto<CustomerDto>(items);
    }

    public async Task<Result<CustomerResponseDto>> CreateAsync(string companyId, CreateCustomerRequestDto request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        var voen = string.IsNullOrWhiteSpace(request.Voen) ? null : request.Voen.Trim();
        var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        var address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();

        if (string.IsNullOrEmpty(name))
            return Result<CustomerResponseDto>.Failure(ErrorType.Validation, "Müştəri adı tələb olunur.");

        if (name.Length > NameMaxLength)
            return Result<CustomerResponseDto>.Failure(ErrorType.Validation, $"Müştəri adı {NameMaxLength} simvoldan uzun ola bilməz.");

        if (voen?.Length > VoenMaxLength)
            return Result<CustomerResponseDto>.Failure(ErrorType.Validation, $"VÖEN {VoenMaxLength} simvoldan uzun ola bilməz.");

        if (phone?.Length > PhoneMaxLength)
            return Result<CustomerResponseDto>.Failure(ErrorType.Validation, $"Telefon {PhoneMaxLength} simvoldan uzun ola bilməz.");

        if (address?.Length > AddressMaxLength)
            return Result<CustomerResponseDto>.Failure(ErrorType.Validation, $"Ünvan {AddressMaxLength} simvoldan uzun ola bilməz.");

        var customer = new Customer { CompanyId = companyId, Name = name, Voen = voen, Phone = phone, Address = address };

        db.Customers.Add(customer);
        await SequentialCode.AssignAndSaveAsync(
            db,
            db.Customers.Where(c => c.CompanyId == companyId).Select(c => c.Code),
            CodePrefix,
            code => customer.Code = code,
            cancellationToken);

        return CustomerResponseDto.From(customer);
    }

    /// <summary>
    /// A system account is never deleted (400). A counterparty that purchases or sales reference is deactivated instead of
    /// removed, so those documents keep it: it leaves the lists and cannot be picked for new documents. One nothing
    /// references is removed.
    /// </summary>
    public async Task<Result<CustomerDeleteResponseDto>> DeleteAsync(string companyId, string id, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(
            c => c.Id == id && c.CompanyId == companyId && c.IsActive, cancellationToken);

        if (customer is null)
        {
            // Says why the lookup missed: no such id, another company's row, or one already deactivated.
            var existing = await db.Customers.AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => new { c.CompanyId, c.IsActive })
                .FirstOrDefaultAsync(cancellationToken);

            logger.LogWarning(
                "Counterparty delete missed: id {CustomerId}, company {CompanyId}. Row exists: {Exists}, row company: {RowCompanyId}, active: {IsActive}",
                id, companyId, existing is not null, existing?.CompanyId, existing?.IsActive);

            return Result<CustomerDeleteResponseDto>.Failure(ErrorType.NotFound, "Kontragent tapılmadı.");
        }

        // Accounts the system creates and finds by key (e.g. "khazar-online") are used by integrations; losing one would
        // break them.
        if (customer.SystemKey is not null)
            return Result<CustomerDeleteResponseDto>.Failure(ErrorType.Validation, "Sistem kontragenti silinə bilməz.");

        // Purchases and sales keep restricted foreign keys to the counterparty.
        var isReferenced =
            await db.Purchases.AnyAsync(p => p.CustomerId == id, cancellationToken) ||
            await db.Sales.AnyAsync(s => s.CustomerId == id, cancellationToken);

        if (isReferenced)
            customer.IsActive = false;
        else
            db.Customers.Remove(customer);

        await db.SaveChangesAsync(cancellationToken);

        await audit.LogAsync(companyId, AuditModule.Finance, AuditAction.Delete,
            isReferenced
                ? $"'{customer.Code} · {customer.Name}' kontragenti silindi (sənədlərdə istifadə olunduğu üçün deaktiv edildi)"
                : $"'{customer.Code} · {customer.Name}' kontragenti silindi",
            cancellationToken);

        return isReferenced
            ? new CustomerDeleteResponseDto(true, "Bu kontragentə bağlı alış/satış sənədləri olduğu üçün silinmədi, deaktiv edildi: sənədlərdə qalır, siyahıda görünmür.")
            : new CustomerDeleteResponseDto(false, "Kontragent uğurla silindi.");
    }
}

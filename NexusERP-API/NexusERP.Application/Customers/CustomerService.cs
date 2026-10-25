using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Customers;

public class CustomerService(IApplicationDbContext db) : ICustomerService
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
            .Select(c => new CustomerDto(c.Id, c.Code, c.Name, c.Voen, c.Phone, c.Address, c.IsActive))
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
}

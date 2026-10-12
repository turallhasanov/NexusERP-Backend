using NexusERP.Application.Common;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Customers;

/// <summary>Fields are nullable so missing values reach our own validation instead of the framework's.</summary>
public sealed record CreateCustomerRequestDto(string? Name, string? Voen, string? Phone, string? Address);

public sealed record CustomerDto(string Id, string Code, string Name, string? Voen, string? Phone, string? Address, bool IsActive);

/// <summary>POST /api/customers → { "ok": true, "id", "code", "name", "voen", "phone", "address", "isActive" }</summary>
public sealed record CustomerResponseDto(
    string Id,
    string Code,
    string Name,
    string? Voen,
    string? Phone,
    string? Address,
    bool IsActive) : SuccessResponse
{
    public static CustomerResponseDto From(Customer customer) =>
        new(customer.Id, customer.Code, customer.Name, customer.Voen, customer.Phone, customer.Address, customer.IsActive);
}

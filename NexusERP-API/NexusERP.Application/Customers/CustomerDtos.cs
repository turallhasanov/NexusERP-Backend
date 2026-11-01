using NexusERP.Application.Common;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Customers;

/// <summary>Fields are nullable so missing values reach our own validation instead of the framework's.</summary>
public sealed record CreateCustomerRequestDto(string? Name, string? Voen, string? Phone, string? Address);

/// <summary><paramref name="IsSystem"/> marks accounts the system created and uses itself; they cannot be deleted.</summary>
public sealed record CustomerDto(
    string Id, string Code, string Name, string? Voen, string? Phone, string? Address, bool IsActive, bool IsSystem);

/// <summary>
/// DELETE /api/customers/{id} → { "ok": true, "deactivated", "message" }. <c>deactivated</c> is true when purchases or sales
/// still reference the counterparty, so it was hidden from the lists instead of removed.
/// </summary>
public sealed record CustomerDeleteResponseDto(bool Deactivated, string Message) : SuccessResponse;

/// <summary>POST /api/customers → { "ok": true, "id", "code", "name", "voen", "phone", "address", "isActive" }</summary>
public sealed record CustomerResponseDto(
    string Id,
    string Code,
    string Name,
    string? Voen,
    string? Phone,
    string? Address,
    bool IsActive,
    bool IsSystem) : SuccessResponse
{
    public static CustomerResponseDto From(Customer customer) =>
        new(customer.Id, customer.Code, customer.Name, customer.Voen, customer.Phone, customer.Address, customer.IsActive,
            customer.SystemKey is not null);
}

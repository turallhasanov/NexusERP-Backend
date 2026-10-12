using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common.Interfaces;

namespace NexusERP.Application.CashRegisters;

/// <summary>
/// Atomic balance changes shared by cash transactions and expenses. Each is a single UPDATE whose WHERE
/// clause carries the limit check, so check and change cannot be split by a concurrent request.
/// Call inside the caller's database transaction so the balance change commits with the record it belongs to.
/// </summary>
internal static class CashRegisterBalance
{
    // Upper bound of the decimal(18,2) balance column.
    public const decimal MaxMoney = 9_999_999_999_999_999.99m;

    /// <summary>False when the new balance would not fit the column.</summary>
    public static async Task<bool> TryIncreaseAsync(IApplicationDbContext db, string companyId, string cashRegisterId, decimal amount, CancellationToken cancellationToken)
    {
        var limit = MaxMoney - amount;

        var updated = await db.CashRegisters
            .Where(c => c.Id == cashRegisterId && c.CompanyId == companyId && c.Balance <= limit)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Balance, c => c.Balance + amount), cancellationToken);

        return updated == 1;
    }

    /// <summary>
    /// False when the balance does not cover the amount, so two payments taking the last money out of a
    /// register at once cannot both succeed and drive the balance negative.
    /// </summary>
    public static async Task<bool> TryDecreaseAsync(IApplicationDbContext db, string companyId, string cashRegisterId, decimal amount, CancellationToken cancellationToken)
    {
        var updated = await db.CashRegisters
            .Where(c => c.Id == cashRegisterId && c.CompanyId == companyId && c.Balance >= amount)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Balance, c => c.Balance - amount), cancellationToken);

        return updated == 1;
    }
}

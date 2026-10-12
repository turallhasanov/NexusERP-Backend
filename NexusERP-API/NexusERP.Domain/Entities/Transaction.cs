using NexusERP.Domain.Enums;

namespace NexusERP.Domain.Entities;

/// <summary>Payment transaction (pul hərəkəti) on a cash register.</summary>
public class Transaction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;

    /// <summary>Register the money comes in to (income) or goes out of (expense, transfer).</summary>
    public string CashRegisterId { get; set; } = string.Empty;

    /// <summary>Receiving register; set for transfers only.</summary>
    public string? TargetCashRegisterId { get; set; }

    public TransactionType TransactionType { get; set; }

    /// <summary>Always positive; the type gives the direction.</summary>
    public decimal Amount { get; set; }

    public string? Description { get; set; }

    /// <summary>Sale this payment is for, if any.</summary>
    public string? SaleId { get; set; }

    /// <summary>Purchase this payment is for, if any.</summary>
    public string? PurchaseId { get; set; }

    /// <summary>Expense that created this transaction, if any. Set by POST /api/expenses only.</summary>
    public string? ExpenseId { get; set; }

    public DateTime TransactionDate { get; set; }

    public CashRegister? CashRegister { get; set; }
    public CashRegister? TargetCashRegister { get; set; }
    public Sale? Sale { get; set; }
    public Purchase? Purchase { get; set; }
    public Expense? Expense { get; set; }
}

namespace NexusERP.Domain.Entities;

/// <summary>Operating expense (xərc) paid from a cash register. Each one has a matching Expense transaction.</summary>
public class Expense
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public string ExpenseCategoryId { get; set; } = string.Empty;
    public string CashRegisterId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string? Note { get; set; }

    public ExpenseCategory? ExpenseCategory { get; set; }
    public CashRegister? CashRegister { get; set; }

    /// <summary>The cash register transaction this expense created (the FK is on the transaction).</summary>
    public Transaction? Transaction { get; set; }
}

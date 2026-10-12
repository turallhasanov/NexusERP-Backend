using System.Linq.Expressions;
using NexusERP.Application.Common;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Expenses;

/// <summary>Body of POST /api/expense-categories. Nullable so missing values reach our own validation.</summary>
public sealed record CreateExpenseCategoryRequestDto(string? Name, string? Description);

/// <summary>Row in GET /api/expense-categories.</summary>
public sealed record ExpenseCategoryDto(string Id, string Name, string? Description, bool IsActive);

/// <summary>POST /api/expense-categories → { "ok": true, "id", "name", "description", "isActive" }</summary>
public sealed record ExpenseCategoryResponseDto(string Id, string Name, string? Description, bool IsActive) : SuccessResponse
{
    public static ExpenseCategoryResponseDto From(ExpenseCategory category) =>
        new(category.Id, category.Name, category.Description, category.IsActive);
}

/// <summary>Compact expense category reference embedded in expenses (categories have no code).</summary>
public sealed record ExpenseCategoryRefDto(string Id, string Name);

/// <summary>Body of POST /api/expenses. <c>expenseDate</c> defaults to now.</summary>
public sealed record CreateExpenseRequestDto(
    string? ExpenseCategoryId,
    string? CashRegisterId,
    decimal? Amount,
    DateTime? ExpenseDate,
    string? Note);

/// <summary>Row in GET /api/expenses.</summary>
public sealed record ExpenseDto(
    string Id,
    decimal Amount,
    DateTime ExpenseDate,
    string? Note,
    string ExpenseCategoryId,
    ExpenseCategoryRefDto ExpenseCategory,
    string CashRegisterId,
    CodeNameDto CashRegister,
    string? TransactionId)
{
    /// <summary>Server-side projection, loads the expense and its references in one query.</summary>
    public static readonly Expression<Func<Expense, ExpenseDto>> Projection = e => new ExpenseDto(
        e.Id,
        e.Amount,
        e.ExpenseDate,
        e.Note,
        e.ExpenseCategoryId,
        new ExpenseCategoryRefDto(e.ExpenseCategory!.Id, e.ExpenseCategory.Name),
        e.CashRegisterId,
        new CodeNameDto(e.CashRegister!.Id, e.CashRegister.Code, e.CashRegister.Name),
        e.Transaction == null ? null : e.Transaction.Id);
}

/// <summary>POST /api/expenses → { "ok": true, "id", "amount", ..., "transactionId" }</summary>
public sealed record ExpenseResponseDto(
    string Id,
    decimal Amount,
    DateTime ExpenseDate,
    string? Note,
    string ExpenseCategoryId,
    ExpenseCategoryRefDto ExpenseCategory,
    string CashRegisterId,
    CodeNameDto CashRegister,
    string? TransactionId) : SuccessResponse
{
    public static ExpenseResponseDto From(ExpenseDto expense) =>
        new(
            expense.Id,
            expense.Amount,
            expense.ExpenseDate,
            expense.Note,
            expense.ExpenseCategoryId,
            expense.ExpenseCategory,
            expense.CashRegisterId,
            expense.CashRegister,
            expense.TransactionId);
}

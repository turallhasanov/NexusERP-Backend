using System.Linq.Expressions;
using NexusERP.Application.Common;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.CashRegisters;

/// <summary>Body of POST /api/cash-registers. Nullable so missing values reach our own validation.</summary>
public sealed record CreateCashRegisterRequestDto(string? Name);

/// <summary>Row in GET /api/cash-registers.</summary>
public sealed record CashRegisterDto(string Id, string Code, string Name, decimal Balance, bool IsActive);

/// <summary>POST /api/cash-registers → { "ok": true, "id", "code", "name", "balance", "isActive" }</summary>
public sealed record CashRegisterResponseDto(string Id, string Code, string Name, decimal Balance, bool IsActive) : SuccessResponse
{
    public static CashRegisterResponseDto From(CashRegister cashRegister) =>
        new(cashRegister.Id, cashRegister.Code, cashRegister.Name, cashRegister.Balance, cashRegister.IsActive);
}

/// <summary>
/// Body of POST /api/cash-registers/transactions. <c>transactionType</c> is "income", "expense" or "transfer";
/// <c>targetCashRegisterId</c> is required for transfers only; <c>transactionDate</c> defaults to now.
/// </summary>
public sealed record CreateTransactionRequestDto(
    string? CashRegisterId,
    TransactionType? TransactionType,
    decimal? Amount,
    string? Description,
    string? SaleId,
    string? PurchaseId,
    string? TargetCashRegisterId,
    DateTime? TransactionDate);

/// <summary>Compact reference to a sale or purchase document.</summary>
public sealed record DocumentRefDto(string Id, string DocumentNumber);

/// <summary>Row in GET /api/cash-registers/transactions.</summary>
public sealed record TransactionDto(
    string Id,
    TransactionType TransactionType,
    decimal Amount,
    string? Description,
    DateTime TransactionDate,
    string CashRegisterId,
    CodeNameDto CashRegister,
    string? TargetCashRegisterId,
    CodeNameDto? TargetCashRegister,
    string? SaleId,
    DocumentRefDto? Sale,
    string? PurchaseId,
    DocumentRefDto? Purchase,
    string? ExpenseId)
{
    /// <summary>Server-side projection, loads the transaction and its references in one query.</summary>
    public static readonly Expression<Func<Transaction, TransactionDto>> Projection = t => new TransactionDto(
        t.Id,
        t.TransactionType,
        t.Amount,
        t.Description,
        t.TransactionDate,
        t.CashRegisterId,
        new CodeNameDto(t.CashRegister!.Id, t.CashRegister.Code, t.CashRegister.Name),
        t.TargetCashRegisterId,
        t.TargetCashRegisterId == null ? null : new CodeNameDto(t.TargetCashRegister!.Id, t.TargetCashRegister.Code, t.TargetCashRegister.Name),
        t.SaleId,
        t.SaleId == null ? null : new DocumentRefDto(t.Sale!.Id, t.Sale.DocumentNumber),
        t.PurchaseId,
        t.PurchaseId == null ? null : new DocumentRefDto(t.Purchase!.Id, t.Purchase.DocumentNumber),
        t.ExpenseId);
}

/// <summary>POST /api/cash-registers/transactions → { "ok": true, "id", "transactionType", "amount", ... }</summary>
public sealed record TransactionResponseDto(
    string Id,
    TransactionType TransactionType,
    decimal Amount,
    string? Description,
    DateTime TransactionDate,
    string CashRegisterId,
    CodeNameDto CashRegister,
    string? TargetCashRegisterId,
    CodeNameDto? TargetCashRegister,
    string? SaleId,
    DocumentRefDto? Sale,
    string? PurchaseId,
    DocumentRefDto? Purchase,
    string? ExpenseId) : SuccessResponse
{
    public static TransactionResponseDto From(TransactionDto transaction) =>
        new(
            transaction.Id,
            transaction.TransactionType,
            transaction.Amount,
            transaction.Description,
            transaction.TransactionDate,
            transaction.CashRegisterId,
            transaction.CashRegister,
            transaction.TargetCashRegisterId,
            transaction.TargetCashRegister,
            transaction.SaleId,
            transaction.Sale,
            transaction.PurchaseId,
            transaction.Purchase,
            transaction.ExpenseId);
}

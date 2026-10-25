using Microsoft.EntityFrameworkCore;
using NexusERP.Application.CashRegisters;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Expenses;

public class ExpenseService(IApplicationDbContext db) : IExpenseService
{
    // Must match the column lengths in ExpenseConfiguration.
    private const int NoteMaxLength = 500;

    // Upper bound of the decimal(18,2) money columns.
    private const decimal MaxMoney = 9_999_999_999_999_999.99m;

    public async Task<Result<ItemsResponseDto<ExpenseDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken)
    {
        var items = await db.Expenses
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId)
            .OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.Id)
            .Select(ExpenseDto.Projection)
            .ToListAsync(cancellationToken);

        return new ItemsResponseDto<ExpenseDto>(items);
    }

    public async Task<Result<ExpenseResponseDto>> CreateAsync(string companyId, CreateExpenseRequestDto request, CancellationToken cancellationToken)
    {
        var expenseCategoryId = request.ExpenseCategoryId?.Trim();
        var cashRegisterId = request.CashRegisterId?.Trim();
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        var storeId = string.IsNullOrWhiteSpace(request.StoreId) ? null : request.StoreId.Trim();

        if (string.IsNullOrEmpty(expenseCategoryId))
            return Result<ExpenseResponseDto>.Failure(ErrorType.Validation, "Xərc kateqoriyası seçilməlidir.");

        if (string.IsNullOrEmpty(cashRegisterId))
            return Result<ExpenseResponseDto>.Failure(ErrorType.Validation, "Kassa seçilməlidir.");

        if (request.Amount is not { } rawAmount)
            return Result<ExpenseResponseDto>.Failure(ErrorType.Validation, "Məbləğ tələb olunur.");

        // Rounded to the column scale first, so what is validated is exactly what gets stored.
        var amount = Math.Round(rawAmount, 2, MidpointRounding.AwayFromZero);

        if (amount <= 0)
            return Result<ExpenseResponseDto>.Failure(ErrorType.Validation, "Məbləğ sıfırdan böyük olmalıdır.");

        if (amount > MaxMoney)
            return Result<ExpenseResponseDto>.Failure(ErrorType.Validation, "Məbləğ həddindən artıq böyükdür.");

        if (note?.Length > NoteMaxLength)
            return Result<ExpenseResponseDto>.Failure(ErrorType.Validation, $"Qeyd {NoteMaxLength} simvoldan uzun ola bilməz.");

        // Scoped to the caller's company, so another company's records look exactly like missing ones.
        var category = await db.ExpenseCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == expenseCategoryId && c.CompanyId == companyId, cancellationToken);

        if (category is null)
            return Result<ExpenseResponseDto>.Failure(ErrorType.NotFound, "Xərc kateqoriyası tapılmadı.");

        if (!category.IsActive)
            return Result<ExpenseResponseDto>.Failure(ErrorType.Validation, "Seçilmiş xərc kateqoriyası aktiv deyil.");

        var cashRegister = await db.CashRegisters
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cashRegisterId && c.CompanyId == companyId, cancellationToken);

        if (cashRegister is null)
            return Result<ExpenseResponseDto>.Failure(ErrorType.NotFound, "Kassa tapılmadı.");

        if (!cashRegister.IsActive)
            return Result<ExpenseResponseDto>.Failure(ErrorType.Validation, "Seçilmiş kassa aktiv deyil.");

        if (storeId is not null && !await db.Stores.AnyAsync(s => s.Id == storeId && s.CompanyId == companyId && s.IsActive, cancellationToken))
            return Result<ExpenseResponseDto>.Failure(ErrorType.NotFound, "Mağaza tapılmadı.");

        // Balance deduction, expense and its transaction commit together or not at all; disposing without Commit rolls back.
        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        if (!await CashRegisterBalance.TryDecreaseAsync(db, companyId, cashRegisterId, amount, cancellationToken))
            return Result<ExpenseResponseDto>.Failure(ErrorType.Validation, "Kassada xərc üçün kifayət qədər vəsait yoxdur.");

        var expenseDate = request.ExpenseDate ?? DateTime.UtcNow;

        var expense = new Expense
        {
            CompanyId = companyId,
            ExpenseCategoryId = category.Id,
            CashRegisterId = cashRegister.Id,
            Amount = amount,
            ExpenseDate = expenseDate,
            Note = note,
            StoreId = storeId
        };

        db.Expenses.Add(expense);
        db.Transactions.Add(new Transaction
        {
            CompanyId = companyId,
            CashRegisterId = cashRegister.Id,
            TransactionType = TransactionType.Expense,
            Amount = amount,
            // Category names are at most 200 characters, so the fallback always fits the 500-character column.
            Description = note ?? $"Xərc: {category.Name}",
            ExpenseId = expense.Id,
            TransactionDate = expenseDate
        });

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        var created = await db.Expenses
            .AsNoTracking()
            .Where(e => e.Id == expense.Id)
            .Select(ExpenseDto.Projection)
            .SingleAsync(cancellationToken);

        return ExpenseResponseDto.From(created);
    }

    /// <summary>
    /// Moves the expense to another store (or makes it company-wide), for store profit. Amount, category and cash
    /// register stay: the money already left that register through the expense's transaction.
    /// </summary>
    public async Task<Result<ExpenseResponseDto>> UpdateStoreAsync(string companyId, string id, UpdateExpenseStoreRequestDto request, CancellationToken cancellationToken)
    {
        var storeId = string.IsNullOrWhiteSpace(request.StoreId) ? null : request.StoreId.Trim();

        var expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == companyId, cancellationToken);

        if (expense is null)
            return Result<ExpenseResponseDto>.Failure(ErrorType.NotFound, "Xərc tapılmadı.");

        // A closed store is fine to keep, but not to move an expense to.
        if (storeId is not null && storeId != expense.StoreId
            && !await db.Stores.AnyAsync(s => s.Id == storeId && s.CompanyId == companyId && s.IsActive, cancellationToken))
            return Result<ExpenseResponseDto>.Failure(ErrorType.NotFound, "Mağaza tapılmadı.");

        expense.StoreId = storeId;
        await db.SaveChangesAsync(cancellationToken);

        var updated = await db.Expenses
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(ExpenseDto.Projection)
            .SingleAsync(cancellationToken);

        return ExpenseResponseDto.From(updated);
    }
}

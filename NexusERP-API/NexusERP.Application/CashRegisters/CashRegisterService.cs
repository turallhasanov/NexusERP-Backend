using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.CashRegisters;

public class CashRegisterService(IApplicationDbContext db) : ICashRegisterService
{
    private const string CodePrefix = "KSA";

    // Must match the column lengths in CashRegisterConfiguration and TransactionConfiguration.
    private const int NameMaxLength = 200;
    private const int DescriptionMaxLength = 500;

    // Upper bound of the decimal(18,2) money columns.
    private const decimal MaxMoney = 9_999_999_999_999_999.99m;

    private const string InsufficientFunds = "Kassada kifayət qədər vəsait yoxdur.";
    private const string BalanceOverflow = "Kassa balansı həddindən artıq böyük olar.";

    public async Task<Result<ItemsResponseDto<CashRegisterDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken)
    {
        var items = await db.CashRegisters
            .AsNoTracking()
            .Where(c => c.CompanyId == companyId)
            .OrderBy(c => c.Code.Length).ThenBy(c => c.Code)
            .Select(c => new CashRegisterDto(c.Id, c.Code, c.Name, c.Balance, c.IsActive))
            .ToListAsync(cancellationToken);

        return new ItemsResponseDto<CashRegisterDto>(items);
    }

    public async Task<Result<CashRegisterResponseDto>> CreateAsync(string companyId, CreateCashRegisterRequestDto request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();

        if (string.IsNullOrEmpty(name))
            return Result<CashRegisterResponseDto>.Failure(ErrorType.Validation, "Kassa adı tələb olunur.");

        if (name.Length > NameMaxLength)
            return Result<CashRegisterResponseDto>.Failure(ErrorType.Validation, $"Kassa adı {NameMaxLength} simvoldan uzun ola bilməz.");

        var cashRegister = new CashRegister { CompanyId = companyId, Name = name };

        db.CashRegisters.Add(cashRegister);
        await SequentialCode.AssignAndSaveAsync(
            db,
            db.CashRegisters.Where(c => c.CompanyId == companyId).Select(c => c.Code),
            CodePrefix,
            code => cashRegister.Code = code,
            cancellationToken);

        return CashRegisterResponseDto.From(cashRegister);
    }

    public async Task<Result<ItemsResponseDto<TransactionDto>>> GetTransactionsAsync(string companyId, string? cashRegisterId, CancellationToken cancellationToken)
    {
        var query = db.Transactions
            .AsNoTracking()
            .Where(t => t.CompanyId == companyId);

        var registerId = cashRegisterId?.Trim();

        if (!string.IsNullOrEmpty(registerId))
        {
            var exists = await db.CashRegisters.AnyAsync(c => c.Id == registerId && c.CompanyId == companyId, cancellationToken);

            if (!exists)
                return Result<ItemsResponseDto<TransactionDto>>.Failure(ErrorType.NotFound, "Kassa tapılmadı.");

            // A register's history includes transfers it received, not only the ones it sent.
            query = query.Where(t => t.CashRegisterId == registerId || t.TargetCashRegisterId == registerId);
        }

        var items = await query
            .OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.Id)
            .Select(TransactionDto.Projection)
            .ToListAsync(cancellationToken);

        return new ItemsResponseDto<TransactionDto>(items);
    }

    public async Task<Result<TransactionResponseDto>> CreateTransactionAsync(string companyId, CreateTransactionRequestDto request, CancellationToken cancellationToken)
    {
        var cashRegisterId = request.CashRegisterId?.Trim();
        var targetCashRegisterId = NullIfEmpty(request.TargetCashRegisterId);
        var saleId = NullIfEmpty(request.SaleId);
        var purchaseId = NullIfEmpty(request.PurchaseId);
        var description = NullIfEmpty(request.Description);

        if (string.IsNullOrEmpty(cashRegisterId))
            return Result<TransactionResponseDto>.Failure(ErrorType.Validation, "Kassa seçilməlidir.");

        if (request.TransactionType is not { } type)
            return Result<TransactionResponseDto>.Failure(ErrorType.Validation, "Əməliyyat növü seçilməlidir.");

        // JSON numbers bind to any int, so 7 would otherwise slip through as a TransactionType.
        if (!Enum.IsDefined(type))
            return Result<TransactionResponseDto>.Failure(ErrorType.Validation, "Əməliyyat növü yanlışdır.");

        if (request.Amount is not { } rawAmount)
            return Result<TransactionResponseDto>.Failure(ErrorType.Validation, "Məbləğ tələb olunur.");

        // Rounded to the column scale first, so what is validated is exactly what gets stored.
        var amount = Math.Round(rawAmount, 2, MidpointRounding.AwayFromZero);

        if (amount <= 0)
            return Result<TransactionResponseDto>.Failure(ErrorType.Validation, "Məbləğ sıfırdan böyük olmalıdır.");

        if (amount > MaxMoney)
            return Result<TransactionResponseDto>.Failure(ErrorType.Validation, "Məbləğ həddindən artıq böyükdür.");

        if (description?.Length > DescriptionMaxLength)
            return Result<TransactionResponseDto>.Failure(ErrorType.Validation, $"Təsvir {DescriptionMaxLength} simvoldan uzun ola bilməz.");

        if (saleId is not null && purchaseId is not null)
            return Result<TransactionResponseDto>.Failure(ErrorType.Validation, "Əməliyyat eyni anda həm satışa, həm alışa bağlı ola bilməz.");

        if (type == TransactionType.Transfer)
        {
            if (targetCashRegisterId is null)
                return Result<TransactionResponseDto>.Failure(ErrorType.Validation, "Köçürmə üçün hədəf kassa seçilməlidir.");

            if (targetCashRegisterId == cashRegisterId)
                return Result<TransactionResponseDto>.Failure(ErrorType.Validation, "Kassadan eyni kassaya köçürmə edilə bilməz.");

            if (saleId is not null || purchaseId is not null)
                return Result<TransactionResponseDto>.Failure(ErrorType.Validation, "Köçürmə satış və ya alış sənədinə bağlı ola bilməz.");
        }
        else if (targetCashRegisterId is not null)
        {
            return Result<TransactionResponseDto>.Failure(ErrorType.Validation, "Hədəf kassa yalnız köçürmə üçün göstərilir.");
        }

        // Scoped to the caller's company, so another company's records look exactly like missing ones.
        var cashRegister = await db.CashRegisters
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cashRegisterId && c.CompanyId == companyId, cancellationToken);

        if (cashRegister is null)
            return Result<TransactionResponseDto>.Failure(ErrorType.NotFound, "Kassa tapılmadı.");

        if (!cashRegister.IsActive)
            return Result<TransactionResponseDto>.Failure(ErrorType.Validation, "Seçilmiş kassa aktiv deyil.");

        if (targetCashRegisterId is not null)
        {
            var target = await db.CashRegisters
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == targetCashRegisterId && c.CompanyId == companyId, cancellationToken);

            if (target is null)
                return Result<TransactionResponseDto>.Failure(ErrorType.NotFound, "Hədəf kassa tapılmadı.");

            if (!target.IsActive)
                return Result<TransactionResponseDto>.Failure(ErrorType.Validation, "Hədəf kassa aktiv deyil.");
        }

        if (saleId is not null && !await db.Sales.AnyAsync(s => s.Id == saleId && s.CompanyId == companyId, cancellationToken))
            return Result<TransactionResponseDto>.Failure(ErrorType.NotFound, "Satış sənədi tapılmadı.");

        if (purchaseId is not null && !await db.Purchases.AnyAsync(p => p.Id == purchaseId && p.CompanyId == companyId, cancellationToken))
            return Result<TransactionResponseDto>.Failure(ErrorType.NotFound, "Alış sənədi tapılmadı.");

        // Balance changes and the transaction row commit together or not at all; disposing without Commit rolls back.
        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var error = type switch
        {
            TransactionType.Income => await TryIncreaseAsync(companyId, cashRegisterId, amount, cancellationToken),
            TransactionType.Expense => await TryDecreaseAsync(companyId, cashRegisterId, amount, cancellationToken),
            _ => await TryTransferAsync(companyId, cashRegisterId, targetCashRegisterId!, amount, cancellationToken)
        };

        if (error is not null)
            return Result<TransactionResponseDto>.Failure(ErrorType.Validation, error);

        var transaction = new Transaction
        {
            CompanyId = companyId,
            CashRegisterId = cashRegisterId,
            TargetCashRegisterId = targetCashRegisterId,
            TransactionType = type,
            Amount = amount,
            Description = description,
            SaleId = saleId,
            PurchaseId = purchaseId,
            TransactionDate = request.TransactionDate ?? DateTime.UtcNow
        };

        db.Transactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        var created = await db.Transactions
            .AsNoTracking()
            .Where(t => t.Id == transaction.Id)
            .Select(TransactionDto.Projection)
            .SingleAsync(cancellationToken);

        return TransactionResponseDto.From(created);
    }

    /// <summary>
    /// Both registers are updated in a fixed (ordinal Id) order, so two opposite transfers between the same
    /// pair lock the rows in the same sequence and cannot deadlock each other.
    /// </summary>
    private async Task<string?> TryTransferAsync(string companyId, string sourceId, string targetId, decimal amount, CancellationToken cancellationToken)
    {
        if (string.CompareOrdinal(sourceId, targetId) < 0)
        {
            return await TryDecreaseAsync(companyId, sourceId, amount, cancellationToken)
                ?? await TryIncreaseAsync(companyId, targetId, amount, cancellationToken);
        }

        return await TryIncreaseAsync(companyId, targetId, amount, cancellationToken)
            ?? await TryDecreaseAsync(companyId, sourceId, amount, cancellationToken);
    }

    /// <summary>
    /// The limit check is part of the UPDATE's WHERE clause, so check and add are one atomic statement.
    /// Returns an error message, or null on success.
    /// </summary>
    private async Task<string?> TryIncreaseAsync(string companyId, string cashRegisterId, decimal amount, CancellationToken cancellationToken)
    {
        var limit = MaxMoney - amount;

        var updated = await db.CashRegisters
            .Where(c => c.Id == cashRegisterId && c.CompanyId == companyId && c.Balance <= limit)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Balance, c => c.Balance + amount), cancellationToken);

        return updated == 1 ? null : BalanceOverflow;
    }

    /// <summary>
    /// The balance check is part of the UPDATE's WHERE clause, so two payments taking the last money out of
    /// a register at once cannot both succeed and drive the balance negative.
    /// Returns an error message, or null on success.
    /// </summary>
    private async Task<string?> TryDecreaseAsync(string companyId, string cashRegisterId, decimal amount, CancellationToken cancellationToken)
    {
        var updated = await db.CashRegisters
            .Where(c => c.Id == cashRegisterId && c.CompanyId == companyId && c.Balance >= amount)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Balance, c => c.Balance - amount), cancellationToken);

        return updated == 1 ? null : InsufficientFunds;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

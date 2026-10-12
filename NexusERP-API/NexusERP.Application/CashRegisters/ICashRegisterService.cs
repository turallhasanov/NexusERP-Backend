using NexusERP.Application.Common;

namespace NexusERP.Application.CashRegisters;

public interface ICashRegisterService
{
    Task<Result<ItemsResponseDto<CashRegisterDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken);
    Task<Result<CashRegisterResponseDto>> CreateAsync(string companyId, CreateCashRegisterRequestDto request, CancellationToken cancellationToken);
    Task<Result<ItemsResponseDto<TransactionDto>>> GetTransactionsAsync(string companyId, string? cashRegisterId, CancellationToken cancellationToken);
    Task<Result<TransactionResponseDto>> CreateTransactionAsync(string companyId, CreateTransactionRequestDto request, CancellationToken cancellationToken);
}

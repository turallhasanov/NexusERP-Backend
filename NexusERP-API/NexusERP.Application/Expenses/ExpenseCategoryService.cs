using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Expenses;

public class ExpenseCategoryService(IApplicationDbContext db) : IExpenseCategoryService
{
    // Must match the column lengths in ExpenseCategoryConfiguration.
    private const int NameMaxLength = 200;
    private const int DescriptionMaxLength = 500;

    public async Task<Result<ItemsResponseDto<ExpenseCategoryDto>>> GetActiveAsync(string companyId, CancellationToken cancellationToken)
    {
        var items = await db.ExpenseCategories
            .AsNoTracking()
            .Where(c => c.CompanyId == companyId && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new ExpenseCategoryDto(c.Id, c.Name, c.Description, c.IsActive))
            .ToListAsync(cancellationToken);

        return new ItemsResponseDto<ExpenseCategoryDto>(items);
    }

    public async Task<Result<ExpenseCategoryResponseDto>> CreateAsync(string companyId, CreateExpenseCategoryRequestDto request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        if (string.IsNullOrEmpty(name))
            return Result<ExpenseCategoryResponseDto>.Failure(ErrorType.Validation, "Xərc kateqoriyasının adı tələb olunur.");

        if (name.Length > NameMaxLength)
            return Result<ExpenseCategoryResponseDto>.Failure(ErrorType.Validation, $"Xərc kateqoriyasının adı {NameMaxLength} simvoldan uzun ola bilməz.");

        if (description?.Length > DescriptionMaxLength)
            return Result<ExpenseCategoryResponseDto>.Failure(ErrorType.Validation, $"Təsvir {DescriptionMaxLength} simvoldan uzun ola bilməz.");

        var category = new ExpenseCategory { CompanyId = companyId, Name = name, Description = description };

        db.ExpenseCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        return ExpenseCategoryResponseDto.From(category);
    }
}

using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Categories;

public class CategoryService(IApplicationDbContext db) : ICategoryService
{
    private const string CodePrefix = "KTG";

    // Must match the column lengths in CategoryConfiguration.
    private const int NameMaxLength = 200;

    public async Task<Result<ItemsResponseDto<CategoryDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken)
    {
        var items = await db.Categories
            .AsNoTracking()
            .Where(c => c.CompanyId == companyId)
            .OrderBy(c => c.Code.Length).ThenBy(c => c.Code)
            .Select(c => new CategoryDto(c.Id, c.Code, c.Name))
            .ToListAsync(cancellationToken);

        return new ItemsResponseDto<CategoryDto>(items);
    }

    public async Task<Result<CategoryResponseDto>> CreateAsync(string companyId, CreateCategoryRequestDto request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();

        if (string.IsNullOrEmpty(name))
            return Result<CategoryResponseDto>.Failure(ErrorType.Validation, "Kateqoriya adı tələb olunur.");

        if (name.Length > NameMaxLength)
            return Result<CategoryResponseDto>.Failure(ErrorType.Validation, $"Kateqoriya adı {NameMaxLength} simvoldan uzun ola bilməz.");

        var category = new Category { CompanyId = companyId, Name = name };

        db.Categories.Add(category);
        await SequentialCode.AssignAndSaveAsync(
            db,
            db.Categories.Where(c => c.CompanyId == companyId).Select(c => c.Code),
            CodePrefix,
            code => category.Code = code,
            cancellationToken);

        return CategoryResponseDto.From(category);
    }
}

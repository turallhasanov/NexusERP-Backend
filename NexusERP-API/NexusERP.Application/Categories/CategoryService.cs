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

    private const string CategoryNotFound = "Kateqoriya tapılmadı.";

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
        var nameError = ValidateName(name);

        if (nameError is not null)
            return Result<CategoryResponseDto>.Failure(ErrorType.Validation, nameError);

        var category = new Category { CompanyId = companyId, Name = name! };

        db.Categories.Add(category);
        await SequentialCode.AssignAndSaveAsync(
            db,
            db.Categories.Where(c => c.CompanyId == companyId).Select(c => c.Code),
            CodePrefix,
            code => category.Code = code,
            cancellationToken);

        return CategoryResponseDto.From(category);
    }

    public async Task<Result<CategoryResponseDto>> UpdateAsync(string companyId, string id, UpdateCategoryRequestDto request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        var nameError = ValidateName(name);

        if (nameError is not null)
            return Result<CategoryResponseDto>.Failure(ErrorType.Validation, nameError);

        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.CompanyId == companyId, cancellationToken);

        if (category is null)
            return Result<CategoryResponseDto>.Failure(ErrorType.NotFound, CategoryNotFound);

        category.Name = name!;
        await db.SaveChangesAsync(cancellationToken);

        return CategoryResponseDto.From(category);
    }

    public async Task<Result<SuccessResponse>> DeleteAsync(string companyId, string id, CancellationToken cancellationToken)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.CompanyId == companyId, cancellationToken);

        if (category is null)
            return Result<SuccessResponse>.Failure(ErrorType.NotFound, CategoryNotFound);

        // Products keep a required, restricted foreign key to their category.
        if (await db.Products.AnyAsync(p => p.CategoryId == id, cancellationToken))
            return Result<SuccessResponse>.Failure(ErrorType.Conflict, "Bu kateqoriyada məhsul var, onu silmək olmaz.");

        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);

        return new SuccessResponse();
    }

    private static string? ValidateName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return "Kateqoriya adı tələb olunur.";

        if (name.Length > NameMaxLength)
            return $"Kateqoriya adı {NameMaxLength} simvoldan uzun ola bilməz.";

        return null;
    }
}

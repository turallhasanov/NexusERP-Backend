using NexusERP.Application.Audit;
using NexusERP.Application.Common;
using NexusERP.Application.Integration;
using NexusERP.Application.Products;
using NexusERP.Application.Tests.TestSupport;
using NexusERP.Infrastructure.Persistence;

namespace NexusERP.Application.Tests.Products;

public sealed class VariantNameTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly Mock<IKhazarProductClient> _khazar = new();

    public VariantNameTests() =>
        _khazar.Setup(k => k.PushProductAsync(It.IsAny<KhazarProductPush>(), It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(KhazarProductResult.Ok);

    public void Dispose() => _database.Dispose();

    [Theory]
    [InlineData("Usaq Hoodie (S) (M)", "Usaq Hoodie (M)")]
    [InlineData("Usaq Hoodie (S)  (M) ", "Usaq Hoodie (M)")]
    [InlineData("Usaq Hoodie (S) (M) (L)", "Usaq Hoodie (L)")]
    [InlineData("Palto (Qara / S) (Qara / M)", "Palto (Qara / M)")]
    [InlineData("Usaq Hoodie (M)", "Usaq Hoodie (M)")]
    [InlineData("Usaq Hoodie", "Usaq Hoodie")]
    [InlineData("Köynək (2-3 yaş) (M)", "Köynək (2-3 yaş) (M)")]
    [InlineData("Köynək (limited 2) (M)", "Köynək (limited 2) (M)")]
    public void Normalize_keeps_only_the_last_variant_group(string input, string expected) =>
        VariantName.Normalize(input).Should().Be(expected);

    private ProductService CreateService(AppDbContext db) =>
        new(db, Mock.Of<IAuditLogger>(), _khazar.Object, Mock.Of<IProductImageFiles>());

    private static CreateProductRequestDto NewProduct(SeededCompany company, string name) =>
        new(company.CategoryId, company.UnitId, name, null, 10m, 20m, true);

    [Fact]
    public async Task CreateAsync_stores_and_sends_a_stacked_size_name_as_one_variant()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);

        var result = await CreateService(db).CreateAsync(
            company.CompanyId, NewProduct(company, "Usaq Hoodie (S) (M)"), null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Usaq Hoodie (M)");
        _khazar.Verify(k => k.PushProductAsync(
            It.Is<KhazarProductPush>(p => p.Name == "Usaq Hoodie (M)"), It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_refuses_a_second_active_product_with_the_same_name()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        var service = CreateService(db);
        (await service.CreateAsync(company.CompanyId, NewProduct(company, "Usaq Hoodie (M)"), null, CancellationToken.None)).IsSuccess.Should().BeTrue();

        var again = await service.CreateAsync(company.CompanyId, NewProduct(company, "Usaq Hoodie (S) (M)"), null, CancellationToken.None);

        again.ErrorType.Should().Be(ErrorType.Conflict);
        again.Error.Should().Contain("Usaq Hoodie (L)");
    }

    [Fact]
    public async Task CreateAsync_accepts_another_size_of_the_same_product()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        var service = CreateService(db);
        await service.CreateAsync(company.CompanyId, NewProduct(company, "Usaq Hoodie (M)"), null, CancellationToken.None);

        var large = await service.CreateAsync(company.CompanyId, NewProduct(company, "Usaq Hoodie (M) (L)"), null, CancellationToken.None);

        large.IsSuccess.Should().BeTrue();
        large.Value!.Name.Should().Be("Usaq Hoodie (L)");
    }
}

using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using NexusERP.Api.Controllers;
using NexusERP.Api.Storage;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Application.Integration;
using NexusERP.Application.Tests.TestSupport;
using NexusERP.Domain.Entities;
using NexusERP.Infrastructure.Persistence;

namespace NexusERP.Api.IntegrationTests.TestSupport;

/// <summary>
/// Hosts the real NexusERP API (Program.cs) in memory. Replaced for the tests: the SQL Server DbContext (SQLite in
/// memory, one database per factory), the stock writer's T-SQL (see <see cref="SqliteStockWriter"/>), the three Khazar
/// HTTP clients (Moq mocks) and the product photo folder (a temp folder). Everything else, including authentication,
/// CORS and the exception handler, is production code.
/// </summary>
/// <remarks>
/// <c>WebApplicationFactory</c> needs any public type of the API assembly; <see cref="CustomersController"/> is used
/// because <c>Program</c> (top-level statements) is internal and Program.cs must not change.
/// </remarks>
public sealed class NexusErpApiFactory : WebApplicationFactory<CustomersController>, IAsyncLifetime
{
    public const string AllowedOrigin = "http://localhost:5174";
    public const string KhazarPassword = "Khazar-Servis-2026!";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly string _uploadsContentRoot = Path.Combine(Path.GetTempPath(), "nexuserp-tests", Guid.NewGuid().ToString("N"));

    // Program.cs reads these while it builds the app, before WebApplicationFactory's own configuration is applied,
    // so they are environment variables (which override appsettings.json). The values are test-only.
    static NexusErpApiFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "DataSource=:memory:");
        Environment.SetEnvironmentVariable("JwtSettings__Secret", "integration-tests-only-signing-key-0123456789");
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", AllowedOrigin);
    }

    public NexusErpApiFactory() => _connection.Open();

    public Mock<IKhazarProductClient> KhazarProducts { get; } = new();
    public Mock<IKhazarOrderClient> KhazarOrders { get; } = new();
    public Mock<IKhazarCatalogClient> KhazarCatalog { get; } = new();

    public TestTenant Tenant { get; private set; } = default!;

    /// <summary>Folder the photo store writes to (instead of the API project's wwwroot).</summary>
    public string UploadsRoot => Path.Combine(_uploadsContentRoot, "wwwroot", "uploads");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not Development: no Swagger, no user secrets, no exception details in 500 responses (the production shape).
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            // No migrations target this context type, so Program's startup migration check finds none pending.
            services.AddScoped<AppDbContext>(_ => new SqliteAppDbContext(options));

            // The production writer's increment is T-SQL; see SqliteStockWriter.
            services.RemoveAll<IStockWriter>();
            services.AddScoped<IStockWriter, SqliteStockWriter>();

            services.RemoveAll<IKhazarProductClient>();
            services.AddSingleton(KhazarProducts.Object);
            services.RemoveAll<IKhazarOrderClient>();
            services.AddSingleton(KhazarOrders.Object);
            services.RemoveAll<IKhazarCatalogClient>();
            services.AddSingleton(KhazarCatalog.Object);

            services.RemoveAll<ProductImageStore>();
            services.AddSingleton(new ProductImageStore(new UploadsEnvironment(_uploadsContentRoot)));
        });
    }

    public async Task InitializeAsync()
    {
        KhazarProducts
            .Setup(k => k.PushProductAsync(It.IsAny<KhazarProductPush>(), It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(KhazarProductResult.Ok);

        // Starts the host (Program.cs runs), then creates the schema and the test company.
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        Tenant = await TestTenant.SeedAsync(db,
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
            scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>());
    }

    /// <summary>A client sending <paramref name="token"/> as its bearer token (none when null).</summary>
    public HttpClient CreateClient(string? token)
    {
        var client = CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Runs <paramref name="action"/> against the test database through a fresh context.</summary>
    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await _connection.DisposeAsync();

        try
        {
            Directory.Delete(_uploadsContentRoot, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    private sealed class UploadsEnvironment(string contentRoot) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = Path.Combine(contentRoot, "wwwroot");
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "NexusERP.Api";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = contentRoot;
        public string EnvironmentName { get; set; } = "Testing";
    }
}

/// <summary>An active company with users in each role, a warehouse, a store, a category and a unit.</summary>
public sealed record TestTenant(
    string CompanyId,
    string WarehouseId,
    string StoreId,
    string CategoryId,
    string UnitId,
    string KhazarServiceEmail,
    string ItAdminToken,
    string AdminToken,
    string StaffToken,
    string NoCompanyToken)
{
    public static async Task<TestTenant> SeedAsync(AppDbContext db, IPasswordHasher hasher, IJwtTokenGenerator tokens)
    {
        var company = new Company { Name = "Khazar MMC", InviteCode = "TESTCODE" };
        var warehouse = new Warehouse { CompanyId = company.Id, Code = "ANB-001", Name = "Mərkəzi anbar" };
        var store = new Store { CompanyId = company.Id, WarehouseId = warehouse.Id, Code = "MAG-001", Name = "Online mağaza" };
        var category = new Category { CompanyId = company.Id, Code = "KAT-001", Name = "Palto" };
        var unit = new Unit { CompanyId = company.Id, Name = "Ədəd", ShortName = "əd." };

        User NewUser(string name, string email, string role, string? companyId) => new()
        {
            CompanyId = companyId, Name = name, Email = email, Role = role, IsEmailConfirmed = true,
            PasswordHash = hasher.Hash(NexusErpApiFactory.KhazarPassword)
        };

        var itAdmin = NewUser("Khazar Servis", "khazar@test.az", "itadmin", company.Id);
        var admin = NewUser("Aysel Admin", "admin@test.az", "admin", company.Id);
        var staff = NewUser("Orxan İşçi", "staff@test.az", "staff", company.Id);
        var loner = NewUser("Şirkətsiz", "loner@test.az", "admin", null);

        db.AddRange(company, warehouse, store, category, unit, itAdmin, admin, staff, loner);
        await db.SaveChangesAsync();

        return new TestTenant(company.Id, warehouse.Id, store.Id, category.Id, unit.Id, itAdmin.Email,
            tokens.GenerateToken(itAdmin), tokens.GenerateToken(admin), tokens.GenerateToken(staff), tokens.GenerateToken(loner));
    }
}

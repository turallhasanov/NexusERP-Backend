using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using NexusERP.Api.IntegrationTests.TestSupport;
using NexusERP.Domain.Entities;

namespace NexusERP.Api.IntegrationTests.Contracts;

/// <summary>
/// The calls Khazar's NexusErpClient and NexusErpTokenProvider make, sent the way they send them, checked against the
/// fields their private response records read (Khazar.Infrastructure/ExternalServices/NexusErpClient.cs). A renamed
/// JSON field or a changed status here breaks stock sync, catalog pull or order push on the Khazar side.
/// </summary>
public sealed class KhazarIntegrationContractTests(NexusErpApiFactory factory) : IClassFixture<NexusErpApiFactory>
{
    private TestTenant Tenant => factory.Tenant;

    /// <summary>Logs in like NexusErpTokenProvider and returns a client carrying that token, like NexusErpAuthHandler.</summary>
    private async Task<HttpClient> KhazarClientAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("api/auth/login",
            new { email = Tenant.KhazarServiceEmail, password = NexusErpApiFactory.KhazarPassword });
        var token = (await login.JsonAsync())["token"]!.GetValue<string>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private Task<Product> AddProductAsync(string barcode, decimal stock, bool isActive = true) =>
        factory.WithDbAsync(async db =>
        {
            var product = new Product
            {
                CompanyId = Tenant.CompanyId, CategoryId = Tenant.CategoryId, UnitId = Tenant.UnitId,
                Code = "MHS-" + Guid.NewGuid().ToString("N")[..6], Name = "Palto " + barcode, Barcode = barcode,
                SalePrice = 149.90m, IsActive = isActive
            };
            db.Products.Add(product);
            if (stock != 0)
                db.Stocks.Add(new Stock { CompanyId = Tenant.CompanyId, WarehouseId = Tenant.WarehouseId, ProductId = product.Id, Quantity = stock });
            await db.SaveChangesAsync();
            return product;
        });

    private static JsonNode Row(JsonObject body, string key, string value) =>
        body["items"]!.AsArray().Single(i => i![key]?.GetValue<string>() == value)!;

    // 1. NexusErpTokenProvider: LoginResponse(Token, User(CompanyId)).
    [Fact]
    public async Task Login_returns_a_token_and_the_users_company()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("api/auth/login",
            new { email = Tenant.KhazarServiceEmail, password = NexusErpApiFactory.KhazarPassword });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.JsonAsync();
        body["token"]!.GetValue<string>().Split('.').Should().HaveCount(3);
        body["user"]!["companyId"]!.GetValue<string>().Should().Be(Tenant.CompanyId);
    }

    [Fact]
    public async Task A_wrong_password_is_401_which_the_token_provider_treats_as_a_failed_login()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("api/auth/login",
            new { email = Tenant.KhazarServiceEmail, password = "wrong" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // 2. GetStockBySkuAsync / GetProductsAsync: ItemsResponse<StockRow(WarehouseId, Product(Id, Barcode), Quantity)>.
    [Fact]
    public async Task Stocks_list_warehouse_product_barcode_and_quantity()
    {
        var product = await AddProductAsync("KHZ-STOCK-01", 7.5m);

        var body = await (await (await KhazarClientAsync()).GetAsync("api/inventory/stocks")).JsonAsync();

        var row = body["items"]!.AsArray().Single(i => i!["product"]!["barcode"]!.GetValue<string>() == "KHZ-STOCK-01")!;
        row["warehouseId"]!.GetValue<string>().Should().Be(Tenant.WarehouseId);
        row["product"]!["id"]!.GetValue<string>().Should().Be(product.Id);
        row["quantity"]!.GetValue<decimal>().Should().Be(7.5m);
    }

    // 3. GetProductsAsync: ItemsResponse<ProductRow(Id, Name, Barcode, SalePrice, IsActive, Category(Name))>.
    [Fact]
    public async Task Products_list_carries_what_the_catalog_pull_reads()
    {
        var product = await AddProductAsync("KHZ-PULL-01", 0m);

        var row = Row(await (await (await KhazarClientAsync()).GetAsync("api/products")).JsonAsync(), "barcode", "KHZ-PULL-01");

        row["id"]!.GetValue<string>().Should().Be(product.Id);
        row["name"]!.GetValue<string>().Should().Be("Palto KHZ-PULL-01");
        row["salePrice"]!.GetValue<decimal>().Should().Be(149.90m);
        row["isActive"]!.GetValue<bool>().Should().BeTrue();
        row["category"]!["name"]!.GetValue<string>().Should().Be("Palto");
    }

    // 4 + 5. FindOrCreateAsync: ItemsResponse<NamedRow(Id, Name)> for categories and units.
    [Theory]
    [InlineData("api/categories", "Palto")]
    [InlineData("api/units", "Ədəd")]
    public async Task Categories_and_units_list_id_and_name(string endpoint, string name)
    {
        var body = await (await (await KhazarClientAsync()).GetAsync(endpoint)).JsonAsync();

        Row(body, "name", name)["id"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Creating_a_missing_category_and_unit_returns_their_id()
    {
        var client = await KhazarClientAsync();

        var category = await client.PostAsJsonAsync("api/categories", new { name = "Gödəkçə" });
        var unit = await client.PostAsJsonAsync("api/units", new { name = "Cüt", shortName = "cüt" });

        category.StatusCode.Should().Be(HttpStatusCode.OK);
        (await category.JsonAsync())["id"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        unit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await unit.JsonAsync())["id"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
    }

    // 6. ResolveStoreIdAsync: ItemsResponse<StoreRow(Id, Name, IsActive)>.
    [Fact]
    public async Task Stores_list_id_name_and_active_flag()
    {
        var row = Row(await (await (await KhazarClientAsync()).GetAsync("api/stores")).JsonAsync(), "id", Tenant.StoreId);

        row["name"]!.GetValue<string>().Should().Be("Online mağaza");
        row["isActive"]!.GetValue<bool>().Should().BeTrue();
    }

    // 7. FindProductIdAsync: ProductLookupResponse(Product(Id)); 404 means "not in NexusERP yet".
    [Fact]
    public async Task Product_by_barcode_returns_its_id_or_404()
    {
        var product = await AddProductAsync("KHZ-FIND-01", 0m);
        var client = await KhazarClientAsync();

        var found = await client.GetAsync("api/products/barcode/" + Uri.EscapeDataString("KHZ-FIND-01"));
        var missing = await client.GetAsync("api/products/barcode/ZZ-NO-SUCH-SKU-000");

        (await found.JsonAsync())["product"]!["id"]!.GetValue<string>().Should().Be(product.Id);
        await missing.ShouldBeErrorAsync(HttpStatusCode.NotFound, "Bu barkodla məhsul tapılmadı.");
    }

    // 8. CreateMissingProductAsync: JSON POST api/products → IdResponse(Id); 409 when the barcode was taken meanwhile.
    [Fact]
    public async Task Creating_a_missing_product_from_json_returns_its_id_and_a_taken_barcode_is_409()
    {
        var client = await KhazarClientAsync();
        var body = new { categoryId = Tenant.CategoryId, unitId = Tenant.UnitId, name = "Palto (Qara / M)", barcode = "KHZ-NEW-01", salePrice = 99m };

        var created = await client.PostAsJsonAsync("api/products", body);
        var again = await client.PostAsJsonAsync("api/products", body);

        created.StatusCode.Should().Be(HttpStatusCode.OK);
        (await created.JsonAsync())["id"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        await again.ShouldBeErrorAsync(HttpStatusCode.Conflict, "Bu barkod artıq başqa məhsulda istifadə olunur.");
    }

    [Fact]
    public async Task An_empty_product_body_is_a_400_with_a_readable_error()
    {
        var response = await (await KhazarClientAsync()).PostAsJsonAsync("api/products", new { });

        await response.ShouldBeErrorAsync(HttpStatusCode.BadRequest, "Məhsul adı tələb olunur.");
    }

    // 9. PushOrderToErpAsync + CancelOrderInErpAsync: POST api/online-orders → SaleCreatedResponse(Id, DocumentNumber),
    //    idempotent on the order number; DELETE api/online-orders/{number}; 404 *with* an error body = "never received".
    [Fact]
    public async Task An_online_order_is_accepted_once_and_can_be_cancelled()
    {
        var product = await AddProductAsync("KHZ-ORDER-01", 10m);
        var client = await KhazarClientAsync();
        var order = new
        {
            externalOrderNumber = "KHZ-T3ST1",
            storeId = Tenant.StoreId,
            warehouseId = (string?)null,
            documentDate = new DateTime(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc),
            customer = new { fullName = "Leyla Həsənova", email = "leyla@example.az", address = "Nizami küç. 10, Bakı AZ1000, Azərbaycan" },
            items = new[] { new { productId = product.Id, quantity = 2m, unitPrice = 149.90m } }
        };

        var first = await client.PostAsJsonAsync("api/online-orders", order);
        var retry = await client.PostAsJsonAsync("api/online-orders", order);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var sale = await first.JsonAsync();
        sale["id"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        sale["documentNumber"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        (await retry.JsonAsync())["id"]!.GetValue<string>().Should().Be(sale["id"]!.GetValue<string>());
        (await StockOfAsync(product.Id)).Should().Be(8m); // approved on arrival, once despite the retry

        var cancelled = await client.DeleteAsync("api/online-orders/" + Uri.EscapeDataString("KHZ-T3ST1"));
        cancelled.StatusCode.Should().Be(HttpStatusCode.OK);
        (await cancelled.JsonAsync())["status"]!.GetValue<string>().Should().Be("cancelled");
        (await StockOfAsync(product.Id)).Should().Be(10m);

        // Khazar asked for the cancellation, so NexusERP must not call Khazar back to cancel it there.
        factory.KhazarOrders.Verify(k => k.CancelOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private Task<decimal> StockOfAsync(string productId) =>
        factory.WithDbAsync(db => db.Stocks.Where(s => s.ProductId == productId).Select(s => s.Quantity).SingleAsync());

    [Fact]
    public async Task Cancelling_an_order_nexus_never_received_is_404_with_an_error_body()
    {
        var response = await (await KhazarClientAsync()).DeleteAsync("api/online-orders/ZZ-NO-SUCH-ORDER-000");

        // NexusErpClient.CancelOrderInErpAsync treats exactly this (404 + error) as "nothing to cancel".
        await response.ShouldBeErrorAsync(HttpStatusCode.NotFound, "Online sifariş tapılmadı.");
    }

    [Fact]
    public async Task Deleting_an_unknown_counterparty_is_404_with_an_error_body()
    {
        var response = await (await KhazarClientAsync()).DeleteAsync("api/customers/00000000-0000-0000-0000-000000000000");

        await response.ShouldBeErrorAsync(HttpStatusCode.NotFound, "Kontragent tapılmadı.");
    }

    [Fact]
    public async Task A_token_without_a_company_gets_403_which_khazar_reports_as_no_company()
    {
        var client = factory.CreateClient(Tenant.NoCompanyToken);

        (await client.GetAsync("api/inventory/stocks")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("api/stores")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

using System.Net;
using Microsoft.EntityFrameworkCore;
using NexusERP.Api.IntegrationTests.TestSupport;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Api.IntegrationTests.Endpoints;

/// <summary>DELETE /api/customers/{id} through the whole pipeline: auth, roles, soft delete and the response shapes.</summary>
public sealed class CustomersEndpointTests(NexusErpApiFactory factory) : IClassFixture<NexusErpApiFactory>
{
    private TestTenant Tenant => factory.Tenant;

    private Task<Customer> AddCustomerAsync(string name, string? systemKey = null) =>
        factory.WithDbAsync(async db =>
        {
            var customer = new Customer
            {
                CompanyId = Tenant.CompanyId, Code = "KNT-" + Guid.NewGuid().ToString("N")[..6], Name = name, SystemKey = systemKey
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            return customer;
        });

    [Fact]
    public async Task Deleting_an_unused_counterparty_removes_it()
    {
        var customer = await AddCustomerAsync("Silinən Kontragent");

        var response = await factory.CreateClient(Tenant.AdminToken).DeleteAsync($"/api/customers/{customer.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.JsonAsync();
        body["ok"]!.GetValue<bool>().Should().BeTrue();
        body["deactivated"]!.GetValue<bool>().Should().BeFalse();
        body["message"]!.GetValue<string>().Should().Be("Kontragent uğurla silindi.");
        (await factory.WithDbAsync(db => db.Customers.AnyAsync(c => c.Id == customer.Id))).Should().BeFalse();
    }

    [Fact]
    public async Task Deleting_a_counterparty_a_purchase_uses_deactivates_it_and_it_stays_in_the_list_flagged_inactive()
    {
        var customer = await AddCustomerAsync("Təchizatçı");
        await factory.WithDbAsync(async db =>
        {
            var product = new Product
            {
                CompanyId = Tenant.CompanyId, CategoryId = Tenant.CategoryId, UnitId = Tenant.UnitId,
                Code = "MHS-" + Guid.NewGuid().ToString("N")[..6], Name = "Palto", Barcode = Guid.NewGuid().ToString("N")[..13]
            };
            var purchase = new Purchase
            {
                CompanyId = Tenant.CompanyId, WarehouseId = Tenant.WarehouseId, CustomerId = customer.Id,
                DocumentNumber = "ALS-" + Guid.NewGuid().ToString("N")[..6], DocumentDate = DateTime.UtcNow, Status = PurchaseStatus.Draft
            };
            purchase.Items.Add(new PurchaseItem { PurchaseId = purchase.Id, LineNumber = 1, ProductId = product.Id, Quantity = 1, UnitPrice = 5, TotalPrice = 5 });
            db.AddRange(product, purchase);
            return await db.SaveChangesAsync();
        });
        var client = factory.CreateClient(Tenant.ItAdminToken);

        var response = await client.DeleteAsync($"/api/customers/{customer.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.JsonAsync())["deactivated"]!.GetValue<bool>().Should().BeTrue();

        var list = await (await client.GetAsync("/api/customers")).JsonAsync();
        var row = list["items"]!.AsArray().Single(i => i!["id"]!.GetValue<string>() == customer.Id)!;
        row["isActive"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task An_unknown_id_is_404_with_the_error_body()
    {
        var response = await factory.CreateClient(Tenant.AdminToken).DeleteAsync($"/api/customers/{Guid.NewGuid()}");

        await response.ShouldBeErrorAsync(HttpStatusCode.NotFound, "Kontragent tapılmadı.");
    }

    [Fact]
    public async Task A_system_counterparty_is_refused_with_400()
    {
        var online = await AddCustomerAsync("Khazar Online Müştəri", systemKey: "khazar-online-" + Guid.NewGuid().ToString("N")[..6]);

        var response = await factory.CreateClient(Tenant.AdminToken).DeleteAsync($"/api/customers/{online.Id}");

        await response.ShouldBeErrorAsync(HttpStatusCode.BadRequest, "Sistem kontragenti silinə bilməz.");
    }

    [Fact]
    public async Task Staff_get_403_and_the_counterparty_stays()
    {
        var customer = await AddCustomerAsync("Qorunan");

        var response = await factory.CreateClient(Tenant.StaffToken).DeleteAsync($"/api/customers/{customer.Id}");

        await response.ShouldBeErrorAsync(HttpStatusCode.Forbidden, "Bu əməliyyat üçün icazəniz yoxdur.");
        (await factory.WithDbAsync(db => db.Customers.AnyAsync(c => c.Id == customer.Id))).Should().BeTrue();
    }

    [Fact]
    public async Task Without_a_token_it_is_401_with_the_error_body()
    {
        var response = await factory.CreateClient(token: null).DeleteAsync($"/api/customers/{Guid.NewGuid()}");

        await response.ShouldBeErrorAsync(HttpStatusCode.Unauthorized, "Giriş tələb olunur.");
    }

    [Fact]
    public async Task A_user_without_a_company_gets_403()
    {
        var response = await factory.CreateClient(Tenant.NoCompanyToken).DeleteAsync($"/api/customers/{Guid.NewGuid()}");

        await response.ShouldBeErrorAsync(HttpStatusCode.Forbidden, "Şirkət seçilməyib");
    }
}

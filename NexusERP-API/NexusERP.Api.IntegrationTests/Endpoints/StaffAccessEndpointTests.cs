using System.Net;
using FluentAssertions;
using NexusERP.Api.IntegrationTests.TestSupport;

namespace NexusERP.Api.IntegrationTests.Endpoints;

/// <summary>An "İşçi" (staff) reaches the warehouse module but not finance, reports, HR, users or the dashboard.</summary>
public sealed class StaffAccessEndpointTests(NexusErpApiFactory factory) : IClassFixture<NexusErpApiFactory>
{
    private TestTenant Tenant => factory.Tenant;

    public static TheoryData<string> ManagerOnlyUrls => new()
    {
        "/api/finance/summary",
        "/api/finance/mizan",
        "/api/reports/inventory-value",
        "/api/reports/profit-loss",
        "/api/dashboard/summary",
        "/api/expenses",
        "/api/expense-categories",
        "/api/cash-registers",
        "/api/employees",
        "/api/users",
        "/api/roles",
    };

    [Theory]
    [MemberData(nameof(ManagerOnlyUrls))]
    public async Task Staff_gets_403(string url) =>
        (await factory.CreateClient(Tenant.StaffToken).GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

    [Theory]
    [MemberData(nameof(ManagerOnlyUrls))]
    public async Task Admin_and_it_admin_are_not_refused(string url)
    {
        (await factory.CreateClient(Tenant.AdminToken).GetAsync(url)).StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        (await factory.CreateClient(Tenant.ItAdminToken).GetAsync(url)).StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Staff_keeps_the_warehouse_module() =>
        (await factory.CreateClient(Tenant.StaffToken).GetAsync("/api/warehouses")).StatusCode.Should().Be(HttpStatusCode.OK);
}

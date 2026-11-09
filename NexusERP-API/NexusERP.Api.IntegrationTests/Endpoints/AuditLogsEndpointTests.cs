using System.Net;
using NexusERP.Api.IntegrationTests.TestSupport;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Api.IntegrationTests.Endpoints;

/// <summary>GET /api/admin/audit-logs: IT admins only, newest first, filters, and entries written by real actions.</summary>
public sealed class AuditLogsEndpointTests(NexusErpApiFactory factory) : IClassFixture<NexusErpApiFactory>
{
    private TestTenant Tenant => factory.Tenant;

    [Fact]
    public async Task A_counterparty_delete_shows_up_in_the_audit_trail()
    {
        var customer = await factory.WithDbAsync(async db =>
        {
            var c = new Customer { CompanyId = Tenant.CompanyId, Code = "KNT-AUD", Name = "Audit Kontragent" };
            db.Customers.Add(c);
            await db.SaveChangesAsync();
            return c;
        });
        var client = factory.CreateClient(Tenant.ItAdminToken);
        (await client.DeleteAsync($"/api/customers/{customer.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.GetAsync("/api/admin/audit-logs?moduleName=finance&actionType=delete");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.JsonAsync();
        body["ok"]!.GetValue<bool>().Should().BeTrue();
        body["page"]!.GetValue<int>().Should().Be(1);
        body["pageSize"]!.GetValue<int>().Should().Be(50);
        var entry = body["items"]!.AsArray().Single(i => i!["description"]!.GetValue<string>().Contains("KNT-AUD"))!;
        entry["description"]!.GetValue<string>().Should().Be("Khazar Servis tərəfindən 'KNT-AUD · Audit Kontragent' kontragenti silindi");
        entry["moduleName"]!.GetValue<string>().Should().Be("finance");
        entry["actionType"]!.GetValue<string>().Should().Be("delete");
        entry["department"]!.GetValue<string>().Should().Be("Maliyyə");
        entry["userRole"]!.GetValue<string>().Should().Be("itadmin");
    }

    [Fact]
    public async Task Entries_come_newest_first_paged_and_only_from_the_callers_company()
    {
        await factory.WithDbAsync(async db =>
        {
            var other = new Company { Name = "Başqa", InviteCode = "OTHERCO1" };
            db.Companies.Add(other);
            for (var i = 1; i <= 3; i++)
                db.AuditLogs.Add(new AuditLog
                {
                    CompanyId = Tenant.CompanyId, UserName = "Test", UserRole = "admin", Department = "Anbar",
                    ModuleName = AuditModule.Inventory, ActionType = AuditAction.Update, Description = $"paged-{i}",
                    CreatedAt = new DateTime(2026, 10, i, 12, 0, 0, DateTimeKind.Utc)
                });
            db.AuditLogs.Add(new AuditLog
            {
                CompanyId = other.Id, UserName = "Yad", UserRole = "admin", Department = "Anbar",
                ModuleName = AuditModule.Inventory, ActionType = AuditAction.Update, Description = "paged-foreign"
            });
            return await db.SaveChangesAsync();
        });

        var body = await (await factory.CreateClient(Tenant.ItAdminToken)
            .GetAsync("/api/admin/audit-logs?department=Anbar&startDate=2026-10-01&endDate=2026-10-31&pageSize=2")).JsonAsync();

        body["total"]!.GetValue<int>().Should().Be(3);
        body["items"]!.AsArray().Select(i => i!["description"]!.GetValue<string>()).Should().Equal("paged-3", "paged-2");
    }

    [Fact]
    public async Task A_start_date_after_the_end_date_is_400()
    {
        var response = await factory.CreateClient(Tenant.ItAdminToken).GetAsync("/api/admin/audit-logs?startDate=2026-11-05&endDate=2026-11-01");

        await response.ShouldBeErrorAsync(HttpStatusCode.BadRequest, "Başlanğıc tarixi son tarixdən sonra ola bilməz.");
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("staff")]
    public async Task Other_roles_get_403(string role)
    {
        var token = role == "admin" ? Tenant.AdminToken : Tenant.StaffToken;

        var response = await factory.CreateClient(token).GetAsync("/api/admin/audit-logs");

        await response.ShouldBeErrorAsync(HttpStatusCode.Forbidden, "Bu əməliyyat üçün icazəniz yoxdur.");
    }

    [Fact]
    public async Task Without_a_token_it_is_401()
    {
        var response = await factory.CreateClient(token: null).GetAsync("/api/admin/audit-logs");

        await response.ShouldBeErrorAsync(HttpStatusCode.Unauthorized, "Giriş tələb olunur.");
    }
}

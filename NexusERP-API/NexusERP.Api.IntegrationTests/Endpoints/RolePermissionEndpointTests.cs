using System.Net;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NexusERP.Api.IntegrationTests.TestSupport;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;
using NexusERP.Infrastructure.Persistence;

namespace NexusERP.Api.IntegrationTests.Endpoints;

/// <summary>A staff member's custom role decides which modules open for them, on /auth/me and on the API.</summary>
public sealed class RolePermissionEndpointTests(NexusErpApiFactory factory) : IClassFixture<NexusErpApiFactory>
{
    private TestTenant Tenant => factory.Tenant;

    private async Task<(User User, string Token)> NewStaffWithRoleAsync(params string[] permissions)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();

        var role = new CustomRole { CompanyId = Tenant.CompanyId, Name = "Rol " + Guid.NewGuid().ToString("N")[..6], Permissions = [.. permissions] };
        var user = new User
        {
            CompanyId = Tenant.CompanyId, Name = "Anbardar", Role = "staff", IsEmailConfirmed = true, IsApproved = true,
            CustomRoleId = role.Id, Email = $"role-{Guid.NewGuid():N}@test.az".ToLowerInvariant()
        };
        db.CustomRoles.Add(role);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (user, tokens.GenerateToken(user));
    }

    [Fact]
    public async Task Me_lists_the_permissions_of_the_users_role()
    {
        var (_, token) = await NewStaffWithRoleAsync("products", "warehouses");

        var response = await factory.CreateClient(token).GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var permissions = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["user"]!["permissions"]!.AsArray();
        permissions.Select(p => p!.GetValue<string>()).Should().BeEquivalentTo("products", "warehouses");
    }

    [Fact]
    public async Task A_module_the_role_ticks_opens_and_one_it_does_not_stays_403()
    {
        var (_, token) = await NewStaffWithRoleAsync("finance");
        var client = factory.CreateClient(token);

        (await client.GetAsync("/api/finance/summary")).StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/expenses")).StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/reports/inventory-value")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/employees")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

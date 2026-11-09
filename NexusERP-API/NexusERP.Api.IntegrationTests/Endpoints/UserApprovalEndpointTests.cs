using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NexusERP.Api.IntegrationTests.TestSupport;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;
using NexusERP.Infrastructure.Persistence;

namespace NexusERP.Api.IntegrationTests.Endpoints;

public class UserApprovalEndpointTests(NexusErpApiFactory factory) : IClassFixture<NexusErpApiFactory>
{
    private TestTenant Tenant => factory.Tenant;

    private async Task<(User User, string Token)> NewUnapprovedStaffAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();

        // What a sign-up that joined with an invite code looks like: in the company, staff, not approved.
        var user = new User
        {
            CompanyId = Tenant.CompanyId, Name = "Yeni İşçi", Role = "staff", IsEmailConfirmed = true, IsApproved = false,
            Email = $"new-{Guid.NewGuid():N}@test.az".ToLowerInvariant()
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (user, tokens.GenerateToken(user));
    }

    [Fact]
    public async Task An_unapproved_user_gets_403_on_company_data()
    {
        var (_, token) = await NewUnapprovedStaffAsync();
        var client = factory.CreateClient(token);

        (await client.GetAsync("/api/customers")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/products")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_unapproved_user_can_still_read_their_own_state()
    {
        var (user, token) = await NewUnapprovedStaffAsync();

        var response = await factory.CreateClient(token).GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        body["user"]!["id"]!.GetValue<string>().Should().Be(user.Id);
        body["user"]!["isApproved"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task Approving_gives_the_role_and_opens_the_data_without_a_new_token()
    {
        var (user, token) = await NewUnapprovedStaffAsync();

        var approve = await factory.CreateClient(Tenant.ItAdminToken)
            .PostAsJsonAsync($"/api/users/{user.Id}/approve", new { role = "staff" });

        approve.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = JsonNode.Parse(await approve.Content.ReadAsStringAsync())!;
        body["user"]!["isApproved"]!.GetValue<bool>().Should().BeTrue();

        (await factory.CreateClient(token).GetAsync("/api/customers")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Approving_needs_a_valid_role_and_a_company_manager()
    {
        var (user, userToken) = await NewUnapprovedStaffAsync();

        (await factory.CreateClient(Tenant.ItAdminToken).PostAsJsonAsync($"/api/users/{user.Id}/approve", new { role = "boss" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await factory.CreateClient(userToken).PostAsJsonAsync($"/api/users/{user.Id}/approve", new { role = "staff" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_seeded_accounts_are_approved()
    {
        (await factory.CreateClient(Tenant.ItAdminToken).GetAsync("/api/customers")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

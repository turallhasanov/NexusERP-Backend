using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexusERP.Api.IntegrationTests.TestSupport;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;
using NexusERP.Infrastructure.Persistence;

namespace NexusERP.Api.IntegrationTests.Endpoints;

/// <summary>User management: listing, deactivating and reactivating company users, and what a deactivated user can still do.</summary>
public sealed class UserStatusEndpointTests(NexusErpApiFactory factory) : IClassFixture<NexusErpApiFactory>
{
    private TestTenant Tenant => factory.Tenant;

    private async Task<(User User, string Token)> NewStaffAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var tokens = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();

        var user = new User
        {
            CompanyId = Tenant.CompanyId, Name = "İşçi " + Guid.NewGuid().ToString("N")[..6],
            Email = $"staff-{Guid.NewGuid():N}@test.az".ToLowerInvariant(), Role = "staff", IsEmailConfirmed = true,
            PasswordHash = hasher.Hash(NexusErpApiFactory.KhazarPassword)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (user, tokens.GenerateToken(user));
    }

    private Task<HttpResponseMessage> SetStatusAsync(string token, string userId, bool? isActive) =>
        factory.CreateClient(token).PatchAsJsonAsync($"/api/users/{userId}/status", new { isActive });

    [Fact]
    public async Task The_user_list_shows_status_and_creation_date()
    {
        var (user, _) = await NewStaffAsync();

        var response = await factory.CreateClient(Tenant.ItAdminToken).GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var row = (await response.JsonAsync())["items"]!.AsArray().Single(i => i!["id"]!.GetValue<string>() == user.Id)!;
        row["isActive"]!.GetValue<bool>().Should().BeTrue();
        row["role"]!.GetValue<string>().Should().Be("staff");
        row["createdAt"]!.GetValue<string>().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task A_deactivated_users_existing_token_is_refused_and_reactivating_restores_it()
    {
        var (user, token) = await NewStaffAsync();
        (await factory.CreateClient(token).GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        var off = await SetStatusAsync(Tenant.ItAdminToken, user.Id, false);

        off.StatusCode.Should().Be(HttpStatusCode.OK);
        (await off.JsonAsync())["user"]!["isActive"]!.GetValue<bool>().Should().BeFalse();
        var refused = await factory.CreateClient(token).GetAsync("/api/auth/me");
        await refused.ShouldBeErrorAsync(HttpStatusCode.Unauthorized, "Hesabınız deaktiv edilib.");

        (await SetStatusAsync(Tenant.ItAdminToken, user.Id, true)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await factory.CreateClient(token).GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_deactivated_user_cannot_sign_in()
    {
        var (user, _) = await NewStaffAsync();
        await SetStatusAsync(Tenant.ItAdminToken, user.Id, false);

        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = user.Email, password = NexusErpApiFactory.KhazarPassword });

        await login.ShouldBeErrorAsync(HttpStatusCode.Unauthorized, "Hesabınız deaktiv edilib.");
    }

    [Fact]
    public async Task Only_it_admins_change_status_and_never_their_own()
    {
        var (user, _) = await NewStaffAsync();

        (await SetStatusAsync(Tenant.AdminToken, user.Id, false)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SetStatusAsync(Tenant.StaffToken, user.Id, false)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var itAdminId = await factory.WithDbAsync(db => db.Users.Where(u => u.Email == Tenant.KhazarServiceEmail).Select(u => u.Id).SingleAsync());
        await (await SetStatusAsync(Tenant.ItAdminToken, itAdminId, false))
            .ShouldBeErrorAsync(HttpStatusCode.Forbidden, "Öz hesabınızı deaktiv edə bilməzsiniz.");
        (await factory.WithDbAsync(db => db.Users.AnyAsync(u => u.Id == user.Id && u.IsActive))).Should().BeTrue();
    }

    [Fact]
    public async Task An_unknown_user_is_404_and_a_missing_status_is_400()
    {
        (await SetStatusAsync(Tenant.ItAdminToken, Guid.NewGuid().ToString(), false)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var (user, _) = await NewStaffAsync();
        (await SetStatusAsync(Tenant.ItAdminToken, user.Id, null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NexusERP.Application.Auth;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;

namespace NexusERP.Infrastructure.Persistence;

/// <summary>
/// The platform administrator account. Sign-up never grants the superadmin role, so it comes from
/// configuration; keep the password in user secrets or an environment variable, not in appsettings.json.
/// </summary>
public class SuperAdminSettings
{
    public const string SectionName = "SuperAdmin";

    /// <summary>Empty: no platform administrator is set up.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Only used when the account is created; changing it later does not change the password.</summary>
    public string Password { get; init; } = string.Empty;

    public string Name { get; init; } = "Platforma administratoru";
}

public static class SuperAdminSeeder
{
    /// <summary>
    /// Creates the configured platform administrator on startup, or promotes the existing account with that
    /// e-mail (matched case-insensitively). A platform administrator belongs to no company, so an account that
    /// is in one is taken out of it (a warning is logged); it can still open that company's read-only view.
    /// </summary>
    public static async Task SeedSuperAdminAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var settings = provider.GetRequiredService<IOptions<SuperAdminSettings>>().Value;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SuperAdminSeeder));

        var email = AccountRules.NormalizeEmail(settings.Email);

        if (string.IsNullOrEmpty(email))
            return;

        if (AccountRules.ValidateEmail(email) is { } emailError)
            throw new InvalidOperationException($"SuperAdmin:Email is invalid: {emailError}");

        var db = provider.GetRequiredService<AppDbContext>();
        // Sign-up stores e-mails lowercased, but older or hand-edited rows may not be.
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email, cancellationToken);

        if (user is { Role: AppRoles.SuperAdmin, CompanyId: null, IsActive: true, IsEmailConfirmed: true })
            return;

        if (user is not null)
        {
            if (user.CompanyId is not null)
            {
                logger.LogWarning(
                    "SuperAdmin:Email {Email} was a {Role} of company {CompanyId}; it is taken out of the company to become the platform administrator",
                    email, user.Role, user.CompanyId);
                user.CompanyId = null;
            }

            user.Role = AppRoles.SuperAdmin;
            user.IsActive = true;
            user.IsEmailConfirmed = true;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Account {Email} is now the platform administrator", email);
            return;
        }

        if (AccountRules.ValidatePassword(settings.Password) is { } passwordError)
            throw new InvalidOperationException($"SuperAdmin:Password is required to create the platform administrator: {passwordError}");

        var name = settings.Name.Trim();

        db.Users.Add(new User
        {
            Name = AccountRules.ValidateName(name) is null ? name : "Platforma administratoru",
            Email = email,
            PasswordHash = provider.GetRequiredService<IPasswordHasher>().Hash(settings.Password),
            Role = AppRoles.SuperAdmin,
            IsEmailConfirmed = true
        });

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created the platform administrator account {Email}", email);
    }
}

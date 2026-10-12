using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Infrastructure.Authentication;
using NexusERP.Infrastructure.Email;
using NexusERP.Infrastructure.Persistence;

namespace NexusERP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration, bool isDevelopment = false)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IStockWriter, StockWriter>();

        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .Validate(s => Encoding.UTF8.GetByteCount(s.Secret) >= 32, "JwtSettings:Secret must be at least 32 bytes.")
            .Validate(s => s.ExpiryMinutes > 0, "JwtSettings:ExpiryMinutes must be positive.")
            .ValidateOnStart();

        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        var emailSection = configuration.GetSection(EmailSettings.SectionName);
        var emailSettings = emailSection.Get<EmailSettings>() ?? new EmailSettings();

        services.AddOptions<EmailSettings>()
            .Bind(emailSection)
            .Validate(s => !s.IsConfigured || !string.IsNullOrWhiteSpace(s.FromEmail), "Email:FromEmail is required when Email:Host is set.")
            .Validate(s => !s.IsConfigured || s.Port is > 0 and <= 65535, "Email:Port must be between 1 and 65535.")
            .ValidateOnStart();

        // No SMTP server: Development logs the e-mail (with its code) so flows can be tried locally;
        // anywhere else sending fails loudly rather than leaking codes into logs.
        if (emailSettings.IsConfigured)
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        else if (isDevelopment)
            services.AddScoped<IEmailSender, LoggingEmailSender>();
        else
            services.AddScoped<IEmailSender, UnconfiguredEmailSender>();

        return services;
    }
}

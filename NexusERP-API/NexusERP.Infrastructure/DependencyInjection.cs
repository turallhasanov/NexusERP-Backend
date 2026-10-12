using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Infrastructure.Authentication;
using NexusERP.Infrastructure.Persistence;

namespace NexusERP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
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

        return services;
    }
}

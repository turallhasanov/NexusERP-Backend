using Microsoft.Extensions.DependencyInjection;
using NexusERP.Application.Auth;
using NexusERP.Application.Companies;

namespace NexusERP.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICompanyService, CompanyService>();

        return services;
    }
}

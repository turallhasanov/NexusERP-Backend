using Microsoft.Extensions.DependencyInjection;
using NexusERP.Application.Auth;
using NexusERP.Application.Companies;
using NexusERP.Application.Customers;
using NexusERP.Application.Stores;
using NexusERP.Application.Warehouses;

namespace NexusERP.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IStoreService, StoreService>();
        services.AddScoped<ICustomerService, CustomerService>();

        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;
using NexusERP.Application.Auth;
using NexusERP.Application.Categories;
using NexusERP.Application.Companies;
using NexusERP.Application.Customers;
using NexusERP.Application.Inventory;
using NexusERP.Application.Products;
using NexusERP.Application.Purchases;
using NexusERP.Application.Sales;
using NexusERP.Application.Stores;
using NexusERP.Application.Units;
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
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IUnitService, UnitService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<ISaleService, SaleService>();

        return services;
    }
}

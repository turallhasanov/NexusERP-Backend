using Microsoft.Extensions.DependencyInjection;
using NexusERP.Application.Auth;
using NexusERP.Application.CashRegisters;
using NexusERP.Application.Categories;
using NexusERP.Application.Companies;
using NexusERP.Application.Customers;
using NexusERP.Application.Dashboard;
using NexusERP.Application.Expenses;
using NexusERP.Application.Inventory;
using NexusERP.Application.Products;
using NexusERP.Application.Purchases;
using NexusERP.Application.Reports;
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
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ICashRegisterService, CashRegisterService>();
        services.AddScoped<IExpenseCategoryService, ExpenseCategoryService>();
        services.AddScoped<IExpenseService, ExpenseService>();

        return services;
    }
}

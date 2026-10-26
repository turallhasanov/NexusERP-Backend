using Microsoft.Extensions.DependencyInjection;
using NexusERP.Application.Admin;
using NexusERP.Application.Audit;
using NexusERP.Application.Auth;
using NexusERP.Application.CashRegisters;
using NexusERP.Application.Categories;
using NexusERP.Application.Companies;
using NexusERP.Application.Customers;
using NexusERP.Application.Dashboard;
using NexusERP.Application.Expenses;
using NexusERP.Application.Employees;
using NexusERP.Application.Finance;
using NexusERP.Application.Support;
using NexusERP.Application.Inventory;
using NexusERP.Application.Products;
using NexusERP.Application.Purchases;
using NexusERP.Application.Reports;
using NexusERP.Application.Roles;
using NexusERP.Application.Sales;
using NexusERP.Application.Stores;
using NexusERP.Application.SystemInfo;
using NexusERP.Application.Units;
using NexusERP.Application.Warehouses;

namespace NexusERP.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOtpVerificationService, OtpVerificationService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IFinanceService, FinanceService>();
        services.AddScoped<ISupportService, SupportService>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<IAuditLogService, AuditLogService>();
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
        services.AddScoped<ISystemInfoService, SystemInfoService>();
        services.AddScoped<IPlatformAdminService, PlatformAdminService>();
        services.AddScoped<IRoleService, RoleService>();

        return services;
    }
}

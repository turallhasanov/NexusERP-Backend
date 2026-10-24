using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexusERP.Application.Auth;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Application.Companies;
using NexusERP.Application.Dashboard;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Admin;

public class PlatformAdminService(
    IApplicationDbContext db,
    IDashboardService dashboardService,
    IJwtTokenGenerator jwtTokenGenerator,
    IPasswordHasher passwordHasher,
    ILogger<PlatformAdminService> logger) : IPlatformAdminService
{
    private const int ActivityLimit = 15;
    private const int TopTenantsLimit = 5;
    private const int SlowDatabaseMs = 1000;

    private const string TenantNotFound = "Şirkət tapılmadı.";
    private const string EmailTaken = "Bu e-poçt ünvanı artıq qeydiyyatdan keçib.";

    public async Task<Result<ItemsResponseDto<TenantDto>>> GetTenantsAsync(CancellationToken cancellationToken)
    {
        var tenants = await db.Companies
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new TenantDto(c.Id, c.Name, c.Voen, c.CreatedAt, c.Status, db.Users.Count(u => u.CompanyId == c.Id)))
            .ToListAsync(cancellationToken);

        return new ItemsResponseDto<TenantDto>(tenants);
    }

    public async Task<Result<TenantDetailsResponseDto>> GetTenantAsync(string tenantId, CancellationToken cancellationToken)
    {
        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == tenantId, cancellationToken);

        if (company is null)
            return Result<TenantDetailsResponseDto>.Failure(ErrorType.NotFound, TenantNotFound);

        var users = await db.Users
            .AsNoTracking()
            .Where(u => u.CompanyId == tenantId)
            .OrderBy(u => u.CreatedAt)
            .Select(u => new TenantUserDto(u.Id, u.Name, u.Email, u.Role, u.IsActive, u.IsEmailConfirmed, u.CreatedAt))
            .ToListAsync(cancellationToken);

        // One count query per table; a DbContext does not support parallel queries.
        var counts = new TenantCountsDto(
            await db.Products.CountAsync(p => p.CompanyId == tenantId, cancellationToken),
            await db.Warehouses.CountAsync(w => w.CompanyId == tenantId, cancellationToken),
            await db.Stores.CountAsync(s => s.CompanyId == tenantId, cancellationToken),
            await db.Customers.CountAsync(c => c.CompanyId == tenantId, cancellationToken),
            await db.Sales.CountAsync(s => s.CompanyId == tenantId, cancellationToken),
            await db.Purchases.CountAsync(p => p.CompanyId == tenantId, cancellationToken),
            await db.Expenses.CountAsync(e => e.CompanyId == tenantId, cancellationToken));

        var activity = await GetRecentActivityAsync(tenantId, cancellationToken);

        return new TenantDetailsResponseDto(new TenantDetailsDto(
            company.Id, company.Name, company.Voen, company.CreatedAt, company.Status, company.InviteCode,
            counts, users, activity));
    }

    public async Task<Result<CreateTenantResponseDto>> CreateTenantAsync(CreateTenantRequestDto request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        var voen = string.IsNullOrWhiteSpace(request.Voen) ? null : request.Voen.Trim();
        var adminName = request.AdminName?.Trim();
        var adminEmail = AccountRules.NormalizeEmail(request.AdminEmail);

        var error = CompanyService.ValidateProfile(name, voen)
            ?? AccountRules.ValidateName(adminName)
            ?? AccountRules.ValidateEmail(adminEmail)
            ?? AccountRules.ValidatePassword(request.AdminPassword);

        if (error is not null)
            return Result<CreateTenantResponseDto>.Failure(ErrorType.Validation, error);

        if (adminEmail == AuthService.DemoEmail || await db.Users.AnyAsync(u => u.Email == adminEmail, cancellationToken))
            return Result<CreateTenantResponseDto>.Failure(ErrorType.Conflict, EmailTaken);

        var company = new Company
        {
            Name = name!,
            Voen = voen,
            InviteCode = await CompanyService.NewInviteCodeAsync(db, cancellationToken)
        };

        var admin = new User
        {
            CompanyId = company.Id,
            Name = adminName!,
            Email = adminEmail!,
            PasswordHash = passwordHasher.Hash(request.AdminPassword!),
            Role = AppRoles.Admin,
            IsEmailConfirmed = true
        };

        // One SaveChanges: the company and its admin are inserted together or not at all.
        db.Companies.Add(company);
        db.Users.Add(admin);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The same address registered at the same moment: the unique index on Email lets only one through.
            if (await db.Users.AsNoTracking().AnyAsync(u => u.Email == adminEmail, cancellationToken))
                return Result<CreateTenantResponseDto>.Failure(ErrorType.Conflict, EmailTaken);

            throw;
        }

        logger.LogInformation("Platform admin created company {CompanyId} with admin {UserId}", company.Id, admin.Id);

        return new CreateTenantResponseDto(
            new TenantDto(company.Id, company.Name, company.Voen, company.CreatedAt, company.Status, 1),
            new TenantUserDto(admin.Id, admin.Name, admin.Email, admin.Role, admin.IsActive, admin.IsEmailConfirmed, admin.CreatedAt));
    }

    public async Task<Result<TenantResponseDto>> UpdateStatusAsync(string tenantId, UpdateTenantStatusRequestDto request, CancellationToken cancellationToken)
    {
        if (request.Status is not { } status || !Enum.IsDefined(status))
            return Result<TenantResponseDto>.Failure(ErrorType.Validation, "Status yanlışdır. Mümkün dəyərlər: active, passive, suspended.");

        var company = await db.Companies.FindAsync([tenantId], cancellationToken);

        if (company is null)
            return Result<TenantResponseDto>.Failure(ErrorType.NotFound, TenantNotFound);

        if (company.Status != status)
        {
            logger.LogInformation("Platform admin changed company {CompanyId} status from {From} to {To}", company.Id, company.Status, status);
            company.Status = status;
            await db.SaveChangesAsync(cancellationToken);
        }

        var userCount = await db.Users.CountAsync(u => u.CompanyId == tenantId, cancellationToken);

        return new TenantResponseDto(new TenantDto(company.Id, company.Name, company.Voen, company.CreatedAt, company.Status, userCount));
    }

    public async Task<Result<DashboardSummaryResponseDto>> GetTenantDashboardSummaryAsync(
        string tenantId, decimal? lowStockThreshold, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        // The dashboard query answers zeros for an unknown id; an admin should be told it does not exist.
        if (!await db.Companies.AnyAsync(c => c.Id == tenantId, cancellationToken))
            return Result<DashboardSummaryResponseDto>.Failure(ErrorType.NotFound, TenantNotFound);

        return await dashboardService.GetSummaryAsync(tenantId, lowStockThreshold, from, to, cancellationToken);
    }

    public async Task<Result<ImpersonationResponseDto>> ImpersonateAsync(string superAdminId, string tenantId, CancellationToken cancellationToken)
    {
        // Re-checked against the database: a role claim stays in a token after the account is demoted or disabled.
        var superAdmin = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == superAdminId, cancellationToken);

        if (superAdmin is not { Role: AppRoles.SuperAdmin, IsActive: true })
            return Result<ImpersonationResponseDto>.Failure(ErrorType.Forbidden, "Bu əməliyyat üçün icazəniz yoxdur.");

        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == tenantId, cancellationToken);

        if (company is null)
            return Result<ImpersonationResponseDto>.Failure(ErrorType.NotFound, TenantNotFound);

        var issued = jwtTokenGenerator.GenerateImpersonationToken(superAdmin, company.Id);

        logger.LogInformation(
            "Platform admin {UserId} opened company {CompanyId} in read-only view until {ExpiresAt:o}",
            superAdmin.Id, company.Id, issued.ExpiresAt);

        return new ImpersonationResponseDto(issued.Token, issued.ExpiresAt, CompanyDto.From(company));
    }

    public async Task<Result<PlatformAnalyticsResponseDto>> GetAnalyticsAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        if (!await db.Database.CanConnectAsync(cancellationToken))
            return Result<PlatformAnalyticsResponseDto>.Failure(ErrorType.ServiceUnavailable, "Verilənlər bazasına qoşulmaq mümkün olmadı.");

        var latencyMs = (int)stopwatch.ElapsedMilliseconds;
        var since = DateTime.UtcNow.AddDays(-30);

        var byStatus = await db.Companies
            .AsNoTracking()
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int CountOf(CompanyStatus status) => byStatus.FirstOrDefault(s => s.Status == status)?.Count ?? 0;

        var newTenants = await db.Companies.CountAsync(c => c.CreatedAt >= since, cancellationToken);
        var totalUsers = await db.Users.CountAsync(u => u.CompanyId != null, cancellationToken);

        // Only approved sales: drafts are not revenue yet.
        var approvedSales = db.Sales.AsNoTracking().Where(s => s.Status == SaleStatus.Approved);
        var totalRevenue = await approvedSales.SumAsync(s => s.TotalAmount, cancellationToken);
        var recentRevenue = await approvedSales.Where(s => s.DocumentDate >= since).SumAsync(s => s.TotalAmount, cancellationToken);

        // Ordered on an anonymous projection: SQL cannot sort by a property of a record built by its constructor.
        var topTenants = (await db.Companies
                .AsNoTracking()
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Status,
                    Revenue = approvedSales.Where(s => s.CompanyId == c.Id).Sum(s => (decimal?)s.TotalAmount) ?? 0
                })
                .OrderByDescending(t => t.Revenue)
                .Take(TopTenantsLimit)
                .ToListAsync(cancellationToken))
            .Select(t => new TenantRevenueDto(t.Id, t.Name, t.Status, t.Revenue))
            .ToList();

        var health = new SystemHealthDto(latencyMs > SlowDatabaseMs ? "slow" : "healthy", latencyMs, DateTime.UtcNow);

        return new PlatformAnalyticsResponseDto(new PlatformAnalyticsDto(
            byStatus.Sum(s => s.Count),
            CountOf(CompanyStatus.Active),
            CountOf(CompanyStatus.Passive),
            CountOf(CompanyStatus.Suspended),
            newTenants,
            totalUsers,
            totalRevenue,
            recentRevenue,
            topTenants,
            health));
    }

    // The company has no audit log; its latest documents are the closest record of what it has been doing.
    private async Task<IReadOnlyList<TenantActivityDto>> GetRecentActivityAsync(string tenantId, CancellationToken cancellationToken)
    {
        var sales = await db.Sales
            .AsNoTracking()
            .Where(s => s.CompanyId == tenantId)
            .OrderByDescending(s => s.DocumentDate)
            .Take(ActivityLimit)
            .Select(s => new { s.DocumentNumber, s.TotalAmount, s.DocumentDate, s.Status })
            .ToListAsync(cancellationToken);

        var purchases = await db.Purchases
            .AsNoTracking()
            .Where(p => p.CompanyId == tenantId)
            .OrderByDescending(p => p.DocumentDate)
            .Take(ActivityLimit)
            .Select(p => new { p.DocumentNumber, p.TotalAmount, p.DocumentDate, p.Status })
            .ToListAsync(cancellationToken);

        var expenses = await db.Expenses
            .AsNoTracking()
            .Where(e => e.CompanyId == tenantId)
            .OrderByDescending(e => e.ExpenseDate)
            .Take(ActivityLimit)
            .Select(e => new { Category = e.ExpenseCategory!.Name, e.Amount, e.ExpenseDate })
            .ToListAsync(cancellationToken);

        return sales
            .Select(s => new TenantActivityDto("sale", s.DocumentNumber, s.TotalAmount, s.DocumentDate, StatusName(s.Status)))
            .Concat(purchases.Select(p => new TenantActivityDto("purchase", p.DocumentNumber, p.TotalAmount, p.DocumentDate, StatusName(p.Status))))
            .Concat(expenses.Select(e => new TenantActivityDto("expense", e.Category, e.Amount, e.ExpenseDate, null)))
            .OrderByDescending(a => a.Date)
            .Take(ActivityLimit)
            .ToList();
    }

    private static string StatusName(SaleStatus status) => status == SaleStatus.Approved ? "approved" : "draft";

    private static string StatusName(PurchaseStatus status) => status == PurchaseStatus.Approved ? "approved" : "draft";
}

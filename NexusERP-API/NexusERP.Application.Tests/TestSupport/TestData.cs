using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;
using NexusERP.Infrastructure.Persistence;

namespace NexusERP.Application.Tests.TestSupport;

/// <summary>Ids of a seeded company and the reference data every document needs.</summary>
public sealed record SeededCompany(string CompanyId, string WarehouseId, string CategoryId, string UnitId);

/// <summary>Rows that satisfy the real schema's foreign keys and unique indexes.</summary>
public static class TestData
{
    public static async Task<SeededCompany> CompanyAsync(AppDbContext db, string name = "Test MMC", string? storefrontName = "Khazar")
    {
        var company = new Company { Name = name, StorefrontName = storefrontName, InviteCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant() };
        var warehouse = new Warehouse { CompanyId = company.Id, Code = "ANB-001", Name = "Mərkəzi anbar" };
        var category = new Category { CompanyId = company.Id, Code = "KAT-001", Name = "Paltar" };
        var unit = new Unit { CompanyId = company.Id, Name = "Ədəd", ShortName = "əd." };

        db.AddRange(company, warehouse, category, unit);
        await db.SaveChangesAsync();

        return new SeededCompany(company.Id, warehouse.Id, category.Id, unit.Id);
    }

    public static async Task<Customer> CustomerAsync(
        AppDbContext db, string companyId, string code, string name, bool isActive = true, string? systemKey = null)
    {
        var customer = new Customer { CompanyId = companyId, Code = code, Name = name, IsActive = isActive, SystemKey = systemKey };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer;
    }

    public static async Task<Product> ProductAsync(
        AppDbContext db, SeededCompany company, string code, string name, string barcode,
        bool showOnWebsite = false, bool isActive = true, decimal salePrice = 10m)
    {
        var product = new Product
        {
            CompanyId = company.CompanyId,
            CategoryId = company.CategoryId,
            UnitId = company.UnitId,
            Code = code,
            Name = name,
            Barcode = barcode,
            SalePrice = salePrice,
            ShowOnWebsite = showOnWebsite,
            IsActive = isActive
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    public static async Task StockAsync(AppDbContext db, SeededCompany company, string productId, decimal quantity, string? warehouseId = null)
    {
        db.Stocks.Add(new Stock
        {
            CompanyId = company.CompanyId,
            WarehouseId = warehouseId ?? company.WarehouseId,
            ProductId = productId,
            Quantity = quantity
        });
        await db.SaveChangesAsync();
    }

    /// <summary>A purchase with one line per (product, quantity, unit price), numbered from 1.</summary>
    public static async Task<Purchase> PurchaseAsync(
        AppDbContext db, SeededCompany company, string customerId, PurchaseStatus status, string documentNumber,
        params (string ProductId, decimal Quantity, decimal UnitPrice)[] lines)
    {
        var purchase = new Purchase
        {
            CompanyId = company.CompanyId,
            WarehouseId = company.WarehouseId,
            CustomerId = customerId,
            DocumentNumber = documentNumber,
            DocumentDate = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc),
            Status = status
        };

        for (var i = 0; i < lines.Length; i++)
        {
            purchase.Items.Add(new PurchaseItem
            {
                PurchaseId = purchase.Id,
                LineNumber = i + 1,
                ProductId = lines[i].ProductId,
                Quantity = lines[i].Quantity,
                UnitPrice = lines[i].UnitPrice,
                TotalPrice = lines[i].Quantity * lines[i].UnitPrice
            });
        }

        purchase.TotalAmount = purchase.Items.Sum(i => i.TotalPrice);
        db.Purchases.Add(purchase);
        await db.SaveChangesAsync();
        return purchase;
    }
}

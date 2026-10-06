using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Inventory;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Identity;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.E2E.Fixtures;

/// <summary>Data the journeys rely on, layered on top of DbSeeder output. Idempotent (safe on an existing E2E DB).</summary>
public static class E2ETestData
{
    public const string CategorySlug = "hoa-chat";
    public const string ProductSlug = "e2e-nuoc-tay-da-nang";
    public const string ProductName = "E2E Nước tẩy đa năng";
    public const string SkuSmall = "E2E-1L";
    public const string SkuLarge = "E2E-5L";
    public const string LocationCode = "E2E";
    public const string ValidLotCode = "E2E-LOT-OK";
    public const string ExpiredLotCode = "E2E-LOT-HET-HAN";

    public const string ServiceCategorySlug = "dich-vu";
    public const string ServiceSlug = "e2e-ve-sinh-dinh-ky";
    public const string ServiceName = "E2E Vệ sinh định kỳ";
    public const string ServiceVariantLabel = "Gói cơ bản";

    public const string BankBin = "970436";
    public const string BankAccount = "0123456789";
    public const string BankAccountName = "CONG TY HARIAN E2E";

    public const string UserPassword = "E2e@12345";

    public static string EmailFor(string role) => $"{role.ToLowerInvariant()}@e2e.local";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        await SeedBankSettingsAsync(db);
        await SeedProductWithStockAsync(db);
        await SeedBookableServiceAsync(db);
        await SeedRoleUsersAsync(sp);
    }

    public static async Task<int> LotQuantityAsync(IServiceProvider services, string sku, string lotCode)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.StockLots.AsNoTracking()
            .Where(l => l.ProductVariant.Sku == sku && l.LotCode == lotCode)
            .Select(l => l.QuantityOnHand)
            .SingleAsync();
    }

    /// <summary>Net quantity moved for an order by movement type (Issue is negative, Restore positive).</summary>
    public static async Task<int> MovementQuantityAsync(IServiceProvider services, string orderNumber, StockMovementType type)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orderId = await db.Orders.AsNoTracking().Where(o => o.OrderNumber == orderNumber).Select(o => o.Id).SingleAsync();
        return await db.StockMovements.AsNoTracking()
            .Where(m => m.OrderId == orderId && m.Type == type)
            .SumAsync(m => m.Quantity);
    }

    private static async Task SeedBookableServiceAsync(AppDbContext db)
    {
        if (await db.Services.AnyAsync(s => s.Slug == ServiceSlug))
            return;

        var category = await db.Categories.FirstOrDefaultAsync(c => c.Slug == ServiceCategorySlug)
                       ?? throw new InvalidOperationException($"DbSeeder category '{ServiceCategorySlug}' missing.");

        db.Services.Add(new Service
        {
            CategoryId = category.Id,
            Slug = ServiceSlug,
            Status = ProductStatus.Published,
            SortOrder = 100,
            CreatedAt = DateTime.UtcNow,
            Translations =
            {
                new ServiceTranslation { LanguageCode = "vi", Name = ServiceName, ShortDescription = "Dịch vụ dùng cho E2E" },
                new ServiceTranslation { LanguageCode = "en", Name = "E2E Periodic cleaning", ShortDescription = "E2E service" },
                new ServiceTranslation { LanguageCode = "ja", Name = "E2E 定期清掃", ShortDescription = "E2E" }
            },
            Variants =
            {
                new ServiceVariant { Sku = "E2E-SVC", VariantLabel = ServiceVariantLabel, Price = 300000m, IsDefault = true, SortOrder = 1, IsActive = true }
            }
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedBankSettingsAsync(AppDbContext db)
    {
        await UpsertSettingAsync(db, "company.bank.bin", BankBin);
        await UpsertSettingAsync(db, "company.bank.account", BankAccount);
        await UpsertSettingAsync(db, "company.bank.account_name", BankAccountName);
        await db.SaveChangesAsync();
    }

    private static async Task UpsertSettingAsync(AppDbContext db, string key, string value)
    {
        var row = await db.SiteSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (row is null)
            db.SiteSettings.Add(new SiteSetting { Key = key, Value = value, Group = "company" });
        else
            row.Value = value;
    }

    private static async Task SeedProductWithStockAsync(AppDbContext db)
    {
        if (await db.Products.AnyAsync(p => p.Slug == ProductSlug))
            return;

        var category = await db.Categories.FirstOrDefaultAsync(c => c.Slug == CategorySlug)
                       ?? throw new InvalidOperationException($"DbSeeder category '{CategorySlug}' missing.");

        var product = new Product
        {
            CategoryId = category.Id,
            Slug = ProductSlug,
            Status = ProductStatus.Published,
            SortOrder = 0, // first on the paginated /products list
            HasVariantSize = true,
            CreatedAt = DateTime.UtcNow,
            Translations =
            {
                new ProductTranslation { LanguageCode = "vi", Name = ProductName, ShortDescription = "Sản phẩm dùng cho E2E" },
                new ProductTranslation { LanguageCode = "en", Name = "E2E Cleaner", ShortDescription = "E2E product" },
                new ProductTranslation { LanguageCode = "ja", Name = "E2E クリーナー", ShortDescription = "E2E" }
            },
            Variants =
            {
                new ProductVariant { Sku = SkuSmall, VariantLabel = "1L", Price = 150000m, IsDefault = true, SortOrder = 1, IsActive = true },
                new ProductVariant { Sku = SkuLarge, VariantLabel = "5L", Price = 600000m, SortOrder = 2, IsActive = true }
            }
        };
        db.Products.Add(product);

        var location = await db.WarehouseLocations.FirstOrDefaultAsync(l => l.Code == LocationCode);
        if (location is null)
        {
            location = new WarehouseLocation { Code = LocationCode, Name = "Kho E2E", SortOrder = 100, IsActive = true };
            db.WarehouseLocations.Add(location);
        }
        await db.SaveChangesAsync();

        // Expired lot sorts first by HSD; FEFO must skip it and pick the valid lot.
        var today = StockExpiry.TodayUtc();
        foreach (var variant in product.Variants)
        {
            AddLot(db, variant, location, ExpiredLotCode, today.AddDays(-30), 50);
            AddLot(db, variant, location, ValidLotCode, today.AddDays(365), 100_000);
            variant.StockQuantity = 100_050;
        }
        await db.SaveChangesAsync();
    }

    private static void AddLot(AppDbContext db, ProductVariant variant, WarehouseLocation location, string code, DateOnly expiry, int qty)
        => db.StockLots.Add(new StockLot
        {
            ProductVariantId = variant.Id,
            WarehouseLocationId = location.Id,
            LotCode = code,
            ExpiryDate = expiry,
            UnitCost = 10000m,
            QuantityOnHand = qty,
            ReceivedAt = DateTime.UtcNow
        });

    private static async Task SeedRoleUsersAsync(IServiceProvider sp)
    {
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var role in AppRoles.Assignable.Where(r => r != AppRoles.SuperAdmin))
        {
            var email = EmailFor(role);
            if (await users.FindByEmailAsync(email) is not null) continue;

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = $"E2E {AppRoles.Label(role)}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var created = await users.CreateAsync(user, UserPassword);
            if (!created.Succeeded)
                throw new InvalidOperationException($"Seed user {email}: {string.Join("; ", created.Errors.Select(e => e.Description))}");
            await users.AddToRoleAsync(user, role);
        }
    }
}

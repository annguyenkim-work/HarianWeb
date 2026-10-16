using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.DealerDiscounts;
using NewHarian.Application.Dealers;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.E2E.Fixtures;

public sealed record E2EDiscountProduct(int Id, string Name, string Sku, string Slug);

public sealed record E2EDealer(int Id, string BusinessName, string TaxCode)
{
    public string OptionLabel => $"{BusinessName} ({TaxCode})";
}

/// <summary>Unique-per-test dealer / discount data (journeys must not share products: a product belongs to one group).</summary>
public static class E2EDealerData
{
    public const string ProvinceCode = "01";
    public const string CommuneCode = "00004";

    public static string Suffix() => Guid.NewGuid().ToString("N")[..8];

    public static string CompanyTaxCode() => "0" + Random.Shared.NextInt64(100_000_000, 999_999_999);

    /// <summary>Published product with one 100.000đ SKU and FEFO stock.</summary>
    public static async Task<E2EDiscountProduct> ProductAsync(IServiceProvider services, string name)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var category = await db.Categories.FirstAsync(c => c.Slug == E2ETestData.CategorySlug);
        var location = await db.WarehouseLocations.FirstAsync(l => l.Code == E2ETestData.LocationCode);
        var suffix = Suffix();
        var fullName = $"{name} {suffix}";
        var product = new Product
        {
            CategoryId = category.Id,
            Slug = $"e2e-dd-{suffix}",
            Status = ProductStatus.Published,
            SortOrder = 500,
            CreatedAt = DateTime.UtcNow,
            Translations =
            {
                new ProductTranslation { LanguageCode = "vi", Name = fullName },
                new ProductTranslation { LanguageCode = "en", Name = fullName },
                new ProductTranslation { LanguageCode = "ja", Name = fullName }
            },
            Variants = { new ProductVariant { Sku = $"E2E-DD-{suffix}".ToUpperInvariant(), VariantLabel = "1kg", Price = 100_000m, IsDefault = true, IsActive = true, StockQuantity = 1000 } }
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var variant = product.Variants.Single();
        db.StockLots.Add(new StockLot
        {
            ProductVariantId = variant.Id,
            WarehouseLocationId = location.Id,
            LotCode = $"E2E-DD-LOT-{suffix}",
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(365),
            UnitCost = 50_000m,
            QuantityOnHand = 1000,
            ReceivedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return new E2EDiscountProduct(product.Id, fullName, variant.Sku, product.Slug);
    }

    public static async Task<E2EDealer> ApprovedCompanyAsync(IServiceProvider services, DealerStatus status = DealerStatus.Approved)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Suffix();
        var dealer = new Dealer
        {
            BusinessType = DealerBusinessType.Company,
            BusinessName = $"Công ty E2E Đại Lý {suffix}",
            TaxCode = CompanyTaxCode(),
            RepresentativeName = "Nguyễn Văn Đại",
            RepresentativeTitle = "Giám đốc",
            FullName = "Lê Liên Hệ",
            Phone = "0912345678",
            Email = $"dealer-{suffix}@e2e.local",
            InvoiceEmail = $"hoadon-{suffix}@e2e.local",
            Address = "12 Đường Đại Lý",
            ProvinceCode = ProvinceCode,
            ProvinceName = "Thành phố Hà Nội",
            CommuneCode = CommuneCode,
            CommuneName = "Phường E2E",
            Status = status,
            ReviewedAt = status == DealerStatus.Approved ? DateTime.UtcNow : null,
        };
        db.Dealers.Add(dealer);
        await db.SaveChangesAsync();
        return new E2EDealer(dealer.Id, dealer.BusinessName, dealer.TaxCode);
    }

    /// <summary>
    /// Stands in for the approval email: stores a code the test knows, hashed with the app's own key.
    /// Returns it formatted as the dealer would receive it.
    /// </summary>
    public static async Task<string> KnownCodeAsync(IServiceProvider services, int dealerId)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var code = DealerAccessCode.Generate();
        var dealer = await db.Dealers.SingleAsync(d => d.Id == dealerId);
        dealer.AccessCodeHash = scope.ServiceProvider.GetRequiredService<IDealerAccessCodeHasher>().Hash(code);
        dealer.AccessCodeLast4 = DealerAccessCode.Last4(code);
        dealer.AccessCodeIssuedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return DealerAccessCode.Format(code);
    }

    public static async Task<Order> OrderAsync(IServiceProvider services, string orderNumber)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders
            .AsNoTracking().SingleAsync(o => o.OrderNumber == orderNumber);
    }

    /// <summary>Báo giá KIRARA: 1-19 / 20-50 / Từ 51; first product 20/25/30, second 18/22/27.</summary>
    public static async Task<int> KiraraGroupAsync(IServiceProvider services, string name, E2EDiscountProduct coral, E2EDiscountProduct mau)
    {
        using var scope = services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAdminDealerDiscountService>();
        DealerDiscountTierInput Tier(int min, int? max, decimal c, decimal m)
            => new() { MinQuantity = min, MaxQuantity = max, Percents = new() { [coral.Id] = c, [mau.Id] = m } };
        var (ok, error, id) = await service.SaveGroupAsync(new DealerDiscountGroupInput
        {
            Name = name,
            ProductIds = [coral.Id, mau.Id],
            Tiers = [Tier(1, 19, 20, 18), Tier(20, 50, 25, 22), Tier(51, null, 30, 27)],
        }, null);
        if (!ok) throw new InvalidOperationException(error);
        return id!.Value;
    }

    public static async Task<IReadOnlyList<OrderItem>> OrderItemsAsync(IServiceProvider services, string orderNumber)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.OrderItems.AsNoTracking()
            .Where(i => i.Order.OrderNumber == orderNumber)
            .OrderBy(i => i.Id)
            .ToListAsync();
    }

    public static async Task<Dealer?> DealerByTaxCodeAsync(IServiceProvider services, string taxCode)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Dealers
            .AsNoTracking().Include(d => d.BusinessLicense)
            .FirstOrDefaultAsync(d => d.TaxCode == taxCode);
    }
}

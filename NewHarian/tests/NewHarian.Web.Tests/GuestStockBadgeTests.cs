using System.Net;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Tests;

public class GuestStockBadgeTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public GuestStockBadgeTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData(24, null, "badge-ok")]
    [InlineData(2, 5, "badge-warn")]
    [InlineData(0, 5, "badge-danger")]
    public async Task Product_card_shows_sku_and_stock_badge(int qty, int? low, string badgeClass)
    {
        var seeded = await SeedAsync(qty, low);
        var client = _factory.CreateClient();
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/products/" + seeded.CategorySlug + "/" + seeded.Slug));

        Assert.Contains("badge-code", html);
        Assert.Contains(seeded.Sku, html);
        Assert.Contains(badgeClass, html);
    }

    private async Task<Seeded> SeedAsync(int qty, int? low)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var slug = "stock-" + suffix;
        var sku = "KA-" + suffix;
        var category = new Category
        {
            Slug = "stock-cat-" + suffix,
            IsActive = true,
            Translations = { new CategoryTranslation { LanguageCode = "vi", Name = "Kho " + suffix } }
        };
        db.Products.Add(new Product
        {
            Category = category,
            Slug = slug,
            Status = ProductStatus.Published,
            Translations = { new ProductTranslation { LanguageCode = "vi", Name = "Hang " + suffix } },
            Variants =
            {
                new ProductVariant
                {
                    Sku = sku,
                    Price = 1000m,
                    IsActive = true,
                    IsDefault = true,
                    VariantLabel = "Chuan",
                    StockQuantity = qty,
                    LowStockThreshold = low
                }
            }
        });
        await db.SaveChangesAsync();
        return new Seeded(category.Slug, slug, sku);
    }

    private sealed record Seeded(string CategorySlug, string Slug, string Sku);
}

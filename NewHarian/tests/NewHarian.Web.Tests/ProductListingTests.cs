using System.Net;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Tests;

public class ProductListingTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public ProductListingTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Products_page_renders_catalog_shell_inside_the_site_chrome()
    {
        var seeded = await SeedAsync(
            ("Son lot " + Guid.NewGuid().ToString("N")[..6], 1_500_000m, 1, true, "Mo ta ngan"));

        var client = _factory.CreateClient();
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/products?tag=" + seeded.TagSlug));
        var css = await client.GetStringAsync("/css/site.css");

        Assert.Contains("site-header", html);
        Assert.Contains("site-footer", html);
        Assert.Contains("catalog-filters", html);
        Assert.Contains("catalog-toolbar", html);
        Assert.Contains("catalog-sort", html);
        Assert.Contains("sort=price-asc", html);
        Assert.Contains("catalog-card", html);
        Assert.Contains("catalog-card__placeholder", html);
        Assert.Contains(seeded.Products[0].Name, html);
        Assert.Contains("1.500.000", html);
        Assert.Contains("Nổi bật", html);
        Assert.Contains(seeded.TagName, html);
        Assert.Contains("aria-labelledby=\"catalog-title\"", html);
        Assert.Contains(".catalog-card__media", css);
        Assert.Contains("aspect-ratio: 1 / 1", css);
        Assert.Contains("background: #f6f5f3", css);
        Assert.Contains("height: 100%", css);
    }

    [Fact]
    public async Task Price_sort_and_minimum_filter_follow_the_displayed_price()
    {
        var seeded = await SeedAsync(
            ("Cheap " + Guid.NewGuid().ToString("N")[..6], 1_000m, 2, false, null),
            ("Pricey " + Guid.NewGuid().ToString("N")[..6], 900_000m, 1, false, null));
        var cheap = seeded.Products[0].Name;
        var pricey = seeded.Products[1].Name;
        var client = _factory.CreateClient();

        var ascending = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/products?tag={seeded.TagSlug}&sort=price-asc"));
        Assert.True(ascending.IndexOf(cheap, StringComparison.Ordinal) < ascending.IndexOf(pricey, StringComparison.Ordinal));

        var descending = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/products?tag={seeded.TagSlug}&sort=price-desc"));
        Assert.True(descending.IndexOf(pricey, StringComparison.Ordinal) < descending.IndexOf(cheap, StringComparison.Ordinal));

        var filtered = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/products?tag={seeded.TagSlug}&min=5000"));
        Assert.Contains(pricey, filtered);
        Assert.DoesNotContain(cheap, filtered);
        Assert.Contains("catalog-chip", filtered);
    }

    [Fact]
    public async Task Name_sort_orders_cards_alphabetically()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var seeded = await SeedAsync(
            ("Zzz " + suffix, 10m, 1, false, null),
            ("Aaa " + suffix, 10m, 2, false, null));
        var client = _factory.CreateClient();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/products?tag={seeded.TagSlug}&sort=name"));

        Assert.True(html.IndexOf("Aaa " + suffix, StringComparison.Ordinal) < html.IndexOf("Zzz " + suffix, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Category_page_keeps_filters_and_paginates()
    {
        var products = Enumerable.Range(1, 13)
            .Select(i => ($"Item{i:00} " + "page", 100m + i, i, false, (string?)null))
            .ToArray();
        var seeded = await SeedAsync(products);
        var client = _factory.CreateClient();

        var page1 = WebUtility.HtmlDecode(await client.GetStringAsync($"/products/{seeded.CategorySlug}?tag={seeded.TagSlug}"));
        var page2 = WebUtility.HtmlDecode(await client.GetStringAsync($"/products/{seeded.CategorySlug}?tag={seeded.TagSlug}&page=2"));

        Assert.Contains(seeded.CategoryName, page1);
        Assert.Contains("Item01 page", page1);
        Assert.DoesNotContain("Item13 page", page1);
        Assert.Contains("catalog-pager", page1);
        Assert.Contains("aria-current=\"page\"", page1);
        Assert.Contains("Item13 page", page2);
        Assert.DoesNotContain("Item01 page", page2);
    }

    [Fact]
    public async Task Recommended_sort_follows_category_order_then_product_order()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var firstInEarly = "Early first " + suffix;
        var secondInEarly = "Early second " + suffix;
        var firstInLate = "Late first " + suffix;
        string tagSlug;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tag = new Tag { Slug = "order-tag-" + suffix, Name = "Order " + suffix };
            tagSlug = tag.Slug;
            var early = new Category
            {
                Slug = "early-cat-" + suffix,
                SortOrder = 1,
                IsActive = true,
                Translations = { new CategoryTranslation { LanguageCode = "vi", Name = "Early " + suffix } }
            };
            var late = new Category
            {
                Slug = "late-cat-" + suffix,
                SortOrder = 2,
                IsActive = true,
                Translations = { new CategoryTranslation { LanguageCode = "vi", Name = "Late " + suffix } }
            };

            db.Products.AddRange(
                MakeProduct(late, firstInLate, 1, tag),
                MakeProduct(early, secondInEarly, 2, tag),
                MakeProduct(early, firstInEarly, 1, tag));
            await db.SaveChangesAsync();
        }

        var html = WebUtility.HtmlDecode(await _factory.CreateClient().GetStringAsync("/products?tag=" + tagSlug));
        var earlyFirst = html.IndexOf(firstInEarly, StringComparison.Ordinal);
        var earlySecond = html.IndexOf(secondInEarly, StringComparison.Ordinal);
        var lateFirst = html.IndexOf(firstInLate, StringComparison.Ordinal);

        Assert.True(earlyFirst >= 0 && earlyFirst < earlySecond && earlySecond < lateFirst);
    }

    [Fact]
    public async Task Help_pages_describe_recommended_sort()
    {
        var guest = WebUtility.HtmlDecode(await _factory.CreateClient().GetStringAsync("/help"));
        Assert.Contains("Trang Sản phẩm sắp xếp thế nào?", guest);
        Assert.Contains("theo thứ tự danh mục, rồi thứ tự sản phẩm trong danh mục", guest);

        var admin = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SuperAdmin);
        var help = WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/help"));
        Assert.Contains("mục", help);
        Assert.Contains("Đề xuất", help);
        Assert.Contains("dùng đúng thứ tự này", help);
    }

    [Fact]
    public async Task Unknown_sort_still_returns_the_listing()
    {
        var seeded = await SeedAsync(("Plain " + Guid.NewGuid().ToString("N")[..6], 20_000m, 1, false, null));
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/products?tag={seeded.TagSlug}&sort=drop");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(seeded.Products[0].Name, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    private async Task<SeededListing> SeedAsync(params (string Name, decimal Price, int SortOrder, bool Featured, string? Short)[] products)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var category = new Category
        {
            Slug = "list-cat-" + suffix,
            IsActive = true,
            Translations = { new CategoryTranslation { LanguageCode = "vi", Name = "Danh muc " + suffix } }
        };
        var tag = new Tag { Slug = "list-tag-" + suffix, Name = "Nhan " + suffix };
        var seeded = new List<SeededProduct>();
        foreach (var (name, price, sortOrder, featured, shortText) in products)
        {
            var slug = "list-prod-" + Guid.NewGuid().ToString("N")[..8];
            db.Products.Add(new Product
            {
                Category = category,
                Slug = slug,
                Status = ProductStatus.Published,
                SortOrder = sortOrder,
                IsFeatured = featured,
                Translations =
                {
                    new ProductTranslation { LanguageCode = "vi", Name = name, ShortDescription = shortText }
                },
                Variants =
                {
                    new ProductVariant
                    {
                        Sku = "SKU-" + slug,
                        Price = price,
                        IsActive = true,
                        IsDefault = true,
                        VariantLabel = "Chuan"
                    }
                },
                ProductTags = { new ProductTag { Tag = tag } }
            });
            seeded.Add(new SeededProduct(name, slug, price));
        }

        await db.SaveChangesAsync();
        return new SeededListing(category.Slug, "Danh muc " + suffix, tag.Slug, tag.Name, seeded);
    }

    private static Product MakeProduct(Category category, string name, int sortOrder, Tag tag)
    {
        var slug = "order-prod-" + Guid.NewGuid().ToString("N")[..8];
        return new Product
        {
            Category = category,
            Slug = slug,
            Status = ProductStatus.Published,
            SortOrder = sortOrder,
            Translations = { new ProductTranslation { LanguageCode = "vi", Name = name } },
            Variants =
            {
                new ProductVariant
                {
                    Sku = "SKU-" + slug,
                    Price = 1000m,
                    IsActive = true,
                    IsDefault = true,
                    VariantLabel = "Chuan"
                }
            },
            ProductTags = { new ProductTag { Tag = tag } }
        };
    }

    private sealed record SeededProduct(string Name, string Slug, decimal Price);
    private sealed record SeededListing(
        string CategorySlug,
        string CategoryName,
        string TagSlug,
        string TagName,
        IReadOnlyList<SeededProduct> Products);
}

using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Formatting;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Tests;

/// <summary>Money display (VNĐ) + binding formatted price strings on Admin product save.</summary>
public class MoneyFormatIntegrationTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public MoneyFormatIntegrationTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Product_edit_form_and_guest_detail_show_vnd_suffix()
    {
        var (productId, categorySlug, productSlug, price) = await SeedProductAsync(1_500_000m);
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SuperAdmin);

        var form = WebUtility.HtmlDecode(await client.GetStringAsync($"/admin/Products/Edit?id={productId}"));
        Assert.Contains(MoneyFormat.Vnd(price), form);
        Assert.Contains("js-money-input", form);
        Assert.Contains("/js/money-format.js", await client.GetStringAsync("/admin/Products"));

        var guest = _factory.CreateClient();
        var detail = WebUtility.HtmlDecode(await guest.GetStringAsync($"/products/{categorySlug}/{productSlug}"));
        Assert.Contains(MoneyFormat.Vnd(price), detail);
        Assert.DoesNotContain($"{price.ToString("N0")}đ", detail);
    }

    [Fact]
    public async Task Product_save_accepts_formatted_vnd_unit_price()
    {
        var (productId, _, _, _) = await SeedProductAsync(100_000m);
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SuperAdmin);
        var editHtml = await client.GetStringAsync($"/admin/Products/Edit?id={productId}");
        var token = AdminLoginHelper.ExtractRequestVerificationToken(editHtml);
        Assert.False(string.IsNullOrEmpty(token));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Products.Include(p => p.Variants).Include(p => p.Translations)
            .SingleAsync(p => p.Id == productId);
        var variant = product.Variants.Single();

        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token!,
            ["Id"] = productId.ToString(),
            ["CategoryId"] = product.CategoryId.ToString(),
            ["Status"] = product.Status.ToString(),
            ["NameVi"] = product.Translations.First(t => t.LanguageCode == "vi").Name,
            ["HasVariantSize"] = "false",
            ["HasVariantColor"] = "false",
            ["Variants[0].Id"] = variant.Id.ToString(),
            ["Variants[0].Sku"] = variant.Sku,
            ["Variants[0].VariantLabel"] = variant.VariantLabel ?? "",
            ["Variants[0].Price"] = "1.500.000 VNĐ",
            ["Variants[0].IsDefault"] = "true",
            ["Variants[0].IsActive"] = "true",
            ["Variants[0].SortOrder"] = "1"
        };

        var response = await client.PostAsync("/admin/Products/Save", new FormUrlEncodedContent(fields));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), body);

        using var verify = _factory.Services.CreateScope();
        var saved = await verify.ServiceProvider.GetRequiredService<AppDbContext>().ProductVariants
            .AsNoTracking().SingleAsync(v => v.Id == variant.Id);
        Assert.Equal(1_500_000m, saved.Price);
    }

    private async Task<(int ProductId, string CategorySlug, string ProductSlug, decimal Price)> SeedProductAsync(decimal price)
    {
        using var scope = _factory.Services.CreateScope();
        await TestUsers.EnsureRolesAsync(scope.ServiceProvider);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var product = new Product
        {
            Category = new Category { Slug = $"money-cat-{suffix}", IsActive = true },
            Slug = $"money-prod-{suffix}",
            Status = ProductStatus.Published,
            Translations = { new ProductTranslation { LanguageCode = "vi", Name = $"SP Money {suffix}" } },
            Variants = { new ProductVariant { Sku = $"MONEY-{suffix}", Price = price, IsActive = true, IsDefault = true } }
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return (product.Id, product.Category.Slug, product.Slug, price);
    }
}

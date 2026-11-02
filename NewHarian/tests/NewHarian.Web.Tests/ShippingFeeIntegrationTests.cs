using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Shipping;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Tests;

/// <summary>Province fee applies even when the order is large. The old free-shipping threshold is ignored.</summary>
public class ShippingFeeIntegrationTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public ShippingFeeIntegrationTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Large_order_still_pays_the_province_fee()
    {
        var (variantId, _) = await DealerTestData.ProductAsync(_factory, "Ship", 1_500_000m);
        var code = DealerTestData.Hanoi(_factory).ProvinceCode;
        await DealerTestData.EnsureShippingProvinceAsync(_factory, code);
        await SetProvinceFeeAsync(code, 45_000m);

        var guest = _factory.CreateClient();
        var token = await DealerTestData.TokenAsync(guest, "/cart");
        var add = await guest.PostAsync("/Cart/Add", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["productVariantId"] = variantId.ToString(),
            ["quantity"] = "1"
        }));
        Assert.True(add.IsSuccessStatusCode, await add.Content.ReadAsStringAsync());

        using var quote = JsonDocument.Parse(await guest.GetStringAsync("/checkout/shipping-fee?provinceCode=" + code));
        Assert.False(quote.RootElement.TryGetProperty("isFree", out _));
        Assert.Equal(45_000m, quote.RootElement.GetProperty("fee").GetDecimal());
        Assert.Equal(1_545_000m, quote.RootElement.GetProperty("total").GetDecimal());

        var checkout = await guest.GetStringAsync("/checkout");
        Assert.DoesNotContain("Miễn phí ship từ", checkout);
        Assert.DoesNotContain("Miễn phí vận chuyển", checkout);
    }

    [Fact]
    public async Task Missing_rate_uses_default_fee()
    {
        var code = "ship-zero-" + DealerTestData.Suffix();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ShippingProvinces.Add(new ShippingProvince
            {
                Code = code,
                NameVi = "Tỉnh chưa có phí",
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        var guest = _factory.CreateClient();
        using var missing = JsonDocument.Parse(await guest.GetStringAsync("/checkout/shipping-fee?provinceCode=" + code));
        Assert.Equal(ShippingFees.DefaultVnd, missing.RootElement.GetProperty("fee").GetDecimal());

        using var unknown = JsonDocument.Parse(await guest.GetStringAsync("/checkout/shipping-fee?provinceCode=khong-co-tinh"));
        Assert.Equal(ShippingFees.DefaultVnd, unknown.RootElement.GetProperty("fee").GetDecimal());
    }

    private async Task SetProvinceFeeAsync(string code, decimal fee)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var province = await db.ShippingProvinces.Include(p => p.Rate).SingleAsync(p => p.Code == code);
        if (province.Rate is null)
            province.Rate = new ShippingRate { Fee = fee };
        else
            province.Rate.Fee = fee;

        var legacy = await db.SiteSettings.FirstOrDefaultAsync(s => s.Key == "shipping.free_threshold");
        if (legacy is null)
        {
            db.SiteSettings.Add(new SiteSetting
            {
                Key = "shipping.free_threshold",
                Value = "1",
                Group = "shipping"
            });
        }
        else
        {
            legacy.Value = "1";
        }

        await db.SaveChangesAsync();
    }
}

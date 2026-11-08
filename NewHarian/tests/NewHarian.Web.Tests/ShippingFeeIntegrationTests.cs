using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Tests;

/// <summary>Shipping is not priced on the website. Older orders still show the fee that was stored.</summary>
public class ShippingFeeIntegrationTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public ShippingFeeIntegrationTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Checkout_quote_is_goods_only_and_the_fee_endpoint_is_gone()
    {
        var (variantId, _) = await DealerTestData.ProductAsync(_factory, "Ship", 1_500_000m);
        var guest = _factory.CreateClient();
        var token = await DealerTestData.TokenAsync(guest, "/cart");
        var add = await guest.PostAsync("/Cart/Add", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["productVariantId"] = variantId.ToString(),
            ["quantity"] = "1"
        }));
        Assert.True(add.IsSuccessStatusCode, await add.Content.ReadAsStringAsync());

        var checkout = WebUtility.HtmlDecode(await guest.GetStringAsync("/checkout"));
        Assert.Contains("data-shipping-pending", checkout);
        Assert.Contains("Sẽ được nhân viên thông báo khi xác nhận đơn", checkout);
        Assert.DoesNotContain("id=\"sum-ship\"", checkout);
        Assert.DoesNotContain("Miễn phí ship từ", checkout);

        using var quote = JsonDocument.Parse(await guest.GetStringAsync("/checkout/quote?promoCode="));
        Assert.False(quote.RootElement.TryGetProperty("fee", out _));
        Assert.Equal(1_500_000m, quote.RootElement.GetProperty("goods").GetDecimal());
        Assert.Equal(1_500_000m, quote.RootElement.GetProperty("total").GetDecimal());

        var gone = await guest.GetAsync("/checkout/shipping-fee?provinceCode=01");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task Placed_order_stores_zero_fee_and_a_stored_fee_still_renders()
    {
        await DealerTestData.EnsureShippingProvinceAsync(_factory, DealerTestData.Hanoi(_factory).ProvinceCode);
        var (variantId, _) = await DealerTestData.ProductAsync(_factory, "NoShip", 200_000m);
        var guest = _factory.CreateClient();
        var token = await DealerTestData.TokenAsync(guest, "/checkout");
        var add = await guest.PostAsync("/Cart/Add", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["productVariantId"] = variantId.ToString(),
            ["quantity"] = "1"
        }));
        Assert.True(add.IsSuccessStatusCode, await add.Content.ReadAsStringAsync());

        var email = $"ship-{DealerTestData.Suffix()}@test.local";
        var confirm = await guest.PostAsync("/checkout", new FormUrlEncodedContent(Draft(token, email)));
        var confirmHtml = WebUtility.HtmlDecode(await confirm.Content.ReadAsStringAsync());
        Assert.EndsWith("/checkout/confirm", confirm.RequestMessage!.RequestUri!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-shipping-pending", confirmHtml);

        var placed = await guest.PostAsync("/checkout/submit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));
        Assert.Contains("/checkout/success/", placed.RequestMessage!.RequestUri!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        var orderNumber = placed.RequestMessage.RequestUri.AbsolutePath.Split('/').Last();
        var success = WebUtility.HtmlDecode(await placed.Content.ReadAsStringAsync());
        Assert.Contains("data-shipping-pending", success);

        int orderId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = await db.Orders.Include(o => o.Payment).SingleAsync(o => o.OrderNumber == orderNumber);
            Assert.Equal(0m, order.ShippingFee);
            Assert.Equal(200_000m, order.SubTotal);
            Assert.Equal(200_000m, order.Total);
            Assert.Equal(200_000m, order.Payment!.Amount);
            orderId = order.Id;
        }

        var admin = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SuperAdmin);
        var detail = WebUtility.HtmlDecode(await admin.GetStringAsync($"/admin/Orders/Detail/{orderId}"));
        Assert.Contains("data-shipping-pending", detail);
        Assert.Contains("sẽ báo khi xác nhận", detail);
        var print = WebUtility.HtmlDecode(await admin.GetStringAsync($"/admin/Orders/Print/{orderId}"));
        Assert.Contains("sẽ báo khi xác nhận", print);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = await db.Orders.SingleAsync(o => o.Id == orderId);
            order.ShippingFee = 45_000m;
            await db.SaveChangesAsync();
        }

        var trackToken = await DealerTestData.TokenAsync(guest, "/orders/track");
        var track = await guest.PostAsync("/orders/track", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = trackToken,
            ["OrderNumber"] = orderNumber,
            ["CustomerEmail"] = email
        }));
        var trackHtml = WebUtility.HtmlDecode(await track.Content.ReadAsStringAsync());
        Assert.Contains("data-shipping-fee=\"45000\"", trackHtml);
        Assert.Contains("45.000 VNĐ", trackHtml);
        Assert.DoesNotContain("data-shipping-pending", trackHtml);
    }

    [Fact]
    public async Task Admin_shipping_screen_and_menu_are_removed()
    {
        var admin = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SuperAdmin);
        var screen = await admin.GetAsync("/admin/Shipping");
        Assert.Equal(HttpStatusCode.NotFound, screen.StatusCode);

        var orders = WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/Orders"));
        Assert.DoesNotContain("Phí ship", orders);
    }

    private Dictionary<string, string> Draft(string token, string email)
    {
        var (province, commune) = DealerTestData.Hanoi(_factory);
        return new()
        {
            ["__RequestVerificationToken"] = token,
            ["CustomerName"] = "Nguyễn Khách Lẻ",
            ["CustomerEmail"] = email,
            ["CustomerPhone"] = "0912345678",
            ["CitizenId"] = "001234567890",
            ["ShippingProvinceCode"] = province,
            ["ShippingCommuneCode"] = commune,
            ["ShippingAddress"] = "12 Hàng Bài",
            ["PaymentMethod"] = "COD"
        };
    }
}

using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Dealers;
using NewHarian.Application.Formatting;
using NewHarian.Application.PromoCodes;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Tests;

/// <summary>Promo codes: admin CRUD for Super Admin / sales manager, retail checkout redemption.</summary>
public class PromoCodeIntegrationTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public PromoCodeIntegrationTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Sales_staff_cannot_open_promo_codes_and_the_menu_hides_them()
    {
        var staff = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesStaff, allowRedirect: false);
        var menu = await staff.GetStringAsync("/admin/Help");
        Assert.DoesNotContain("href=\"/admin/PromoCodes\"", menu, StringComparison.OrdinalIgnoreCase);

        var denied = await staff.GetAsync("/admin/PromoCodes");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("/admin/access-denied", denied.Headers.Location?.OriginalString ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sales_manager_can_create_update_and_delete_a_code()
    {
        var manager = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesManager);
        var page = await manager.GetStringAsync("/admin/Help");
        Assert.Contains("href=\"/admin/PromoCodes\"", page, StringComparison.OrdinalIgnoreCase);

        var list = await manager.GetStringAsync("/admin/PromoCodes");
        Assert.Contains("id=\"btn-add-promo\"", list);
        var token = AdminLoginHelper.ExtractRequestVerificationToken(list)!;
        var code = "KM" + DealerTestData.Suffix().ToUpperInvariant();

        var created = await DealerTestData.JsonAsync(await manager.PostAsync("/admin/PromoCodes/Save", Fields(token, code)));
        Assert.True(created.GetProperty("ok").GetBoolean(), created.ToString());
        Assert.Equal("Đã thêm mã giảm giá.", created.GetProperty("message").GetString());

        var after = WebUtility.HtmlDecode(await manager.GetStringAsync("/admin/PromoCodes"));
        Assert.Contains($"data-promo-code=\"{code}\"", after);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.PromoCodes.AsNoTracking().SingleAsync(p => p.Code == code);
            Assert.True(row.IsActive, $"from={row.ValidFrom:o} to={row.ValidTo:o} used={row.UsedCount}/{row.MaxUses} now={DateTime.UtcNow:o}");
            Assert.InRange(DateTime.UtcNow, row.ValidFrom, row.ValidTo);
        }
        Assert.Contains("Đang dùng", after);

        var duplicate = await manager.PostAsync("/admin/PromoCodes/Save", Fields(token, code.ToLowerInvariant()));
        var duplicateHtml = WebUtility.HtmlDecode(await duplicate.Content.ReadAsStringAsync());
        Assert.Contains(PromoCodeRules.Duplicate, duplicateHtml);

        var zero = await manager.PostAsync("/admin/PromoCodes/Save", Fields(token, code + "X", maxUses: "0"));
        Assert.Contains(PromoCodeRules.QuantityInvalid, WebUtility.HtmlDecode(await zero.Content.ReadAsStringAsync()));

        int id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            id = await db.PromoCodes.Where(p => p.Code == code).Select(p => p.Id).SingleAsync();
        }

        var deleted = await manager.PostAsync("/admin/PromoCodes/Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["id"] = id.ToString()
        }));
        Assert.True(deleted.IsSuccessStatusCode, await deleted.Content.ReadAsStringAsync());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.PromoCodes.AnyAsync(p => p.Code == code));
        }
    }

    [Fact]
    public async Task Retail_checkout_applies_percent_once_and_a_second_order_is_rejected_until_cancel()
    {
        await DealerTestData.EnsureShippingProvinceAsync(_factory, DealerTestData.Hanoi(_factory).ProvinceCode);
        var (variantId, _) = await DealerTestData.ProductAsync(_factory, "Son", 200_000);
        var code = await SaveCodeAsync("SALE", 10, maxUses: 1);

        var guest = _factory.CreateClient();
        var token = await DealerTestData.TokenAsync(guest, "/dealers/register");
        await AddAsync(guest, token, variantId, 1);
        var form = WebUtility.HtmlDecode(await guest.GetStringAsync("/checkout"));
        Assert.Contains("name=\"PromoCode\"", form);

        var email = $"promo-{DealerTestData.Suffix()}@test.local";
        var confirm = await guest.PostAsync("/checkout", new FormUrlEncodedContent(Draft(token, email, code, "BankTransfer")));
        var confirmHtml = WebUtility.HtmlDecode(await confirm.Content.ReadAsStringAsync());
        Assert.EndsWith("/checkout/confirm", confirm.RequestMessage!.RequestUri!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-confirm-promo", confirmHtml);
        Assert.Contains($"data-promo-code=\"{code}\"", confirmHtml);
        Assert.Contains("-20.000 VNĐ", confirmHtml);

        var placed = await guest.PostAsync("/checkout/submit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));
        Assert.Contains("/checkout/success/", placed.RequestMessage!.RequestUri!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        var orderNumber = placed.RequestMessage.RequestUri.AbsolutePath.Split('/').Last();
        var success = WebUtility.HtmlDecode(await placed.Content.ReadAsStringAsync());
        Assert.Contains("data-success-promo", success);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = await db.Orders.AsNoTracking().SingleAsync(o => o.OrderNumber == orderNumber);
            Assert.Null(order.DealerId);
            Assert.Equal(code, order.PromoCode);
            Assert.Equal(10m, order.PromoDiscountPercent);
            Assert.Equal(20_000m, order.DiscountAmount);
            Assert.Equal(200_000m, order.SubTotal);
            Assert.Equal(0m, order.ShippingFee);
            Assert.Equal(180_000m, order.Total);
            Assert.Equal(1, await db.PromoCodes.Where(p => p.Code == code).Select(p => p.UsedCount).SingleAsync());
        }

        await AddAsync(guest, token, variantId, 1);
        var blocked = await guest.PostAsync("/checkout", new FormUrlEncodedContent(Draft(token, email, code, "COD")));
        Assert.Contains(PromoCodeRules.Exhausted, WebUtility.HtmlDecode(await blocked.Content.ReadAsStringAsync()));

        var cancelToken = await DealerTestData.TokenAsync(guest, "/orders/track");
        var cancel = await guest.PostAsync("/orders/track/cancel", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = cancelToken,
            ["orderNumber"] = orderNumber,
            ["customerEmail"] = email
        }));
        Assert.Contains("Đã hủy đơn hàng.", WebUtility.HtmlDecode(await cancel.Content.ReadAsStringAsync()));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(0, await db.PromoCodes.Where(p => p.Code == code).Select(p => p.UsedCount).SingleAsync());
        }

        var again = await guest.PostAsync("/checkout", new FormUrlEncodedContent(Draft(token, email, code, "COD")));
        Assert.EndsWith("/checkout/confirm", again.RequestMessage!.RequestUri!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dealer_checkout_rejects_a_promo_code()
    {
        await DealerTestData.EnsureShippingProvinceAsync(_factory, DealerTestData.Hanoi(_factory).ProvinceCode);
        var (variantId, _) = await DealerTestData.ProductAsync(_factory, "Son dai ly", 150_000);
        var code = await SaveCodeAsync("DL", 15, maxUses: 5);
        var dealer = await DealerTestData.DealerAsync(_factory);
        var access = DealerAccessCode.Generate();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.Dealers.SingleAsync(d => d.Id == dealer.Id);
            row.AccessCodeHash = scope.ServiceProvider.GetRequiredService<IDealerAccessCodeHasher>().Hash(access);
            row.AccessCodeIssuedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var guest = _factory.CreateClient();
        var token = await DealerTestData.TokenAsync(guest, "/dealers/register");
        await AddAsync(guest, token, variantId, 1);
        using var enter = new HttpRequestMessage(HttpMethod.Post, "/checkout/dealer-code")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["code"] = access,
                ["returnUrl"] = "/checkout"
            })
        };
        enter.Headers.Add("Accept", "application/json");
        var entered = await DealerTestData.JsonAsync(await guest.SendAsync(enter));
        Assert.True(entered.GetProperty("ok").GetBoolean(), entered.ToString());

        var checkout = WebUtility.HtmlDecode(await guest.GetStringAsync("/checkout"));
        Assert.DoesNotContain("name=\"PromoCode\"", checkout);

        var response = await guest.PostAsync("/checkout", new FormUrlEncodedContent(
            Draft(token, dealer.Email, code, "COD")));
        Assert.Contains(PromoCodeRules.RetailOnly, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db2.PromoCodes.Where(p => p.Code == code).Select(p => p.UsedCount).SingleAsync());
        Assert.False(await db2.Orders.AnyAsync(o => o.CustomerEmail == dealer.Email && o.PromoCode == code));
    }

    [Fact]
    public async Task Cancelling_from_admin_returns_the_use()
    {
        await DealerTestData.EnsureShippingProvinceAsync(_factory, DealerTestData.Hanoi(_factory).ProvinceCode);
        var (variantId, _) = await DealerTestData.ProductAsync(_factory, "Son huy", 100_000);
        var code = await SaveCodeAsync("HUY", 20, maxUses: 1);
        var guest = _factory.CreateClient();
        var token = await DealerTestData.TokenAsync(guest, "/dealers/register");
        await AddAsync(guest, token, variantId, 1);
        var email = $"huy-{DealerTestData.Suffix()}@test.local";
        await guest.PostAsync("/checkout", new FormUrlEncodedContent(Draft(token, email, code, "COD")));
        var placed = await guest.PostAsync("/checkout/submit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));
        var orderNumber = placed.RequestMessage!.RequestUri!.AbsolutePath.Split('/').Last();

        int orderId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            orderId = await db.Orders.Where(o => o.OrderNumber == orderNumber).Select(o => o.Id).SingleAsync();
            Assert.Equal(1, await db.PromoCodes.Where(p => p.Code == code).Select(p => p.UsedCount).SingleAsync());
        }

        var manager = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesManager);
        var detail = WebUtility.HtmlDecode(await manager.GetStringAsync($"/admin/Orders/Detail/{orderId}"));
        Assert.Contains("data-order-promo", detail);
        Assert.Contains(code, detail);

        var adminToken = await DealerTestData.TokenAsync(manager, "/admin/Orders");
        int promoId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            promoId = await db.PromoCodes.Where(p => p.Code == code).Select(p => p.Id).SingleAsync();
        }
        var blockedDelete = await manager.PostAsync("/admin/PromoCodes/Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = adminToken,
            ["id"] = promoId.ToString()
        }));
        Assert.Contains(PromoCodeRules.DeleteUsed, WebUtility.HtmlDecode(await blockedDelete.Content.ReadAsStringAsync()));

        var updated = await DealerTestData.JsonAsync(await manager.PostAsync("/admin/Orders/UpdateStatus", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = adminToken,
            ["id"] = orderId.ToString(),
            ["status"] = OrderStatus.Cancelled.ToString()
        })));
        Assert.True(updated.GetProperty("ok").GetBoolean(), updated.ToString());

        using var check = _factory.Services.CreateScope();
        var dbCheck = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await dbCheck.PromoCodes.Where(p => p.Code == code).Select(p => p.UsedCount).SingleAsync());
    }

    private async Task<string> SaveCodeAsync(string prefix, decimal percent, int maxUses)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAdminPromoCodeService>();
        var local = VietnamTime.ToLocal(DateTime.UtcNow);
        var code = prefix + DealerTestData.Suffix().ToUpperInvariant();
        var (ok, error, _) = await service.SaveAsync(new PromoCodeSaveRequest
        {
            Code = code,
            DiscountPercent = percent,
            ValidFromLocal = local.AddDays(-1).ToString("yyyy-MM-ddTHH:mm"),
            ValidToLocal = local.AddDays(7).ToString("yyyy-MM-ddTHH:mm"),
            MaxUses = maxUses,
            IsActive = true
        });
        Assert.True(ok, error);
        return code;
    }

    private static FormUrlEncodedContent Fields(string token, string code, string maxUses = "5")
    {
        var local = VietnamTime.ToLocal(DateTime.UtcNow);
        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Code"] = code,
            ["DiscountPercent"] = "10",
            ["ValidFromLocal"] = local.AddHours(-1).ToString("yyyy-MM-ddTHH:mm"),
            ["ValidToLocal"] = local.AddDays(3).ToString("yyyy-MM-ddTHH:mm"),
            ["MaxUses"] = maxUses,
            ["IsActive"] = "true"
        });
    }

    private Dictionary<string, string> Draft(string token, string email, string promo, string payment)
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
            ["PaymentMethod"] = payment,
            ["PromoCode"] = promo
        };
    }

    private static async Task AddAsync(HttpClient guest, string token, int variantId, int quantity)
    {
        var response = await guest.PostAsync("/Cart/Add", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["productVariantId"] = variantId.ToString(),
            ["quantity"] = quantity.ToString()
        }));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }
}

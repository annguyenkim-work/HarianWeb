using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Dealers;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Email;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Tests;

/// <summary>Dealer access code: issue on approve, reissue, guest code entry at cart/checkout, dealer order snapshot. Own fixture.</summary>
public class DealerAccessCodeIntegrationTests : IClassFixture<DealerCodeWebApplicationFactory>
{
    private const string Invalid = "Mã đại lý không hợp lệ.";
    private const string Expired = "Mã đại lý đã hết hiệu lực. Vui lòng nhập lại mã.";

    private readonly DealerCodeWebApplicationFactory _factory;

    public DealerAccessCodeIntegrationTests(DealerCodeWebApplicationFactory factory) => _factory = factory;

    private sealed record Shop(Dealer Dealer, int CoralVariant, string CoralSku, int MauVariant, string MauSku);

    /// <summary>Approved dealer with an issued code + KIRARA group (Coral 20/25/30, Màu 18/22/27, 100.000đ).</summary>
    private async Task<(Shop Shop, string Code)> SetupAsync(DealerBusinessType type = DealerBusinessType.Company)
    {
        var (coral, coralSku) = await DealerTestData.ProductAsync(_factory, "Coral", 100_000m);
        var (mau, mauSku) = await DealerTestData.ProductAsync(_factory, "Màu", 100_000m);
        await DealerTestData.GroupAsync(_factory, "KIRARA " + DealerTestData.Suffix(), [coral, mau], [20m, 18m], [25m, 22m], [30m, 27m]);
        var dealer = await DealerTestData.DealerAsync(_factory, type: type);
        await DealerTestData.EnsureShippingProvinceAsync(_factory, DealerTestData.Hanoi(_factory).ProvinceCode);
        var code = await IssueAsync(dealer);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var coralVariant = await db.ProductVariants.Where(v => v.Sku == coralSku).Select(v => v.Id).SingleAsync();
        var mauVariant = await db.ProductVariants.Where(v => v.Sku == mauSku).Select(v => v.Id).SingleAsync();
        return (new Shop(dealer, coralVariant, coralSku, mauVariant, mauSku), code);
    }

    private async Task<string> IssueAsync(Dealer dealer)
    {
        using var scope = _factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IDealerAccessCodeService>().IssueAsync(dealer.Id, null);
        Assert.True(result.Ok, result.Error);
        return _factory.LastCodeFor(dealer.Email);
    }

    private async Task<Dealer> DealerAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Dealers.AsNoTracking().SingleAsync(d => d.Id == id);
    }

    private static string ErrorOn(string html)
    {
        var decoded = WebUtility.HtmlDecode(html);
        var at = decoded.IndexOf("class=\"text-danger\"", StringComparison.Ordinal);
        return at < 0 ? "no error shown" : decoded.Substring(at, Math.Min(300, decoded.Length - at));
    }

    private static async Task<string> GuestTokenAsync(HttpClient guest) => await DealerTestData.TokenAsync(guest, "/dealers/register");

    private static async Task AddToCartAsync(HttpClient guest, string token, int variantId, int quantity)
    {
        var response = await guest.PostAsync("/Cart/Add", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["productVariantId"] = variantId.ToString(),
            ["quantity"] = quantity.ToString(),
        }));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> EnterCodeAsync(HttpClient guest, string token, string code)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/checkout/dealer-code")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["code"] = code,
                ["returnUrl"] = "/cart",
            })
        };
        request.Headers.Add("Accept", "application/json");
        return await DealerTestData.JsonAsync(await guest.SendAsync(request));
    }

    private Dictionary<string, string> DraftFields(string token, string email, string citizenId = "")
    {
        var (province, commune) = DealerTestData.Hanoi(_factory);
        return new()
        {
            ["__RequestVerificationToken"] = token,
            ["CustomerName"] = "Lê Liên Hệ",
            ["CustomerEmail"] = email,
            ["CustomerPhone"] = "0912345678",
            ["CitizenId"] = citizenId,
            ["ShippingProvinceCode"] = province,
            ["ShippingCommuneCode"] = commune,
            ["ShippingAddress"] = "99 Kho Đại Lý",
            ["PaymentMethod"] = "COD",
        };
    }

    [Fact]
    public async Task Approve_issues_code_stores_only_hash_and_queues_sensitive_email()
    {
        var pending = await DealerTestData.DealerAsync(_factory, DealerStatus.Pending);
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesManager);
        var token = await DealerTestData.TokenAsync(client, "/admin/Dealers");

        var json = await DealerTestData.JsonAsync(await client.PostAsync("/admin/Dealers/Approve",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["id"] = pending.Id.ToString() })));
        Assert.True(json.GetProperty("ok").GetBoolean(), json.ToString());
        Assert.Equal("Đã duyệt đại lý và gửi mã đại lý qua email.", json.GetProperty("message").GetString());

        var code = _factory.LastCodeFor(pending.Email);
        var normalized = DealerAccessCode.Normalize(code)!;
        var dealer = await DealerAsync(pending.Id);
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        Assert.Equal(sp.GetRequiredService<IDealerAccessCodeHasher>().Hash(normalized), dealer.AccessCodeHash);
        Assert.Equal(normalized[^4..], dealer.AccessCodeLast4);
        Assert.NotNull(dealer.AccessCodeIssuedAt);
        Assert.NotNull(dealer.AccessCodeIssuedByUserId);

        var db = sp.GetRequiredService<AppDbContext>();
        Assert.False(await db.Dealers.AnyAsync(d => d.AccessCodeHash!.Contains(normalized)));
        var outbox = await db.EmailOutboxMessages.AsNoTracking().Where(m => m.ToAddress == pending.Email).ToListAsync();
        var mail = Assert.Single(outbox);
        Assert.True(mail.IsSensitive);

        var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.Action == "Dealer.CodeIssued" && a.EntityId == pending.Id.ToString());
        Assert.Contains(dealer.AccessCodeLast4!, audit.NewValues);
        Assert.DoesNotContain(normalized, audit.NewValues);
        Assert.DoesNotContain(dealer.AccessCodeHash!, audit.NewValues);
        Assert.False(await db.AppLogEntries.AnyAsync(l => l.Message.Contains(normalized) || l.Message.Contains(code)));

        var detail = WebUtility.HtmlDecode(await client.GetStringAsync($"/admin/Dealers/Detail/{pending.Id}"));
        Assert.Contains(DealerAccessCode.Masked(dealer.AccessCodeLast4!), detail);
        Assert.DoesNotContain(code, detail);
        Assert.Contains("data-action=\"issue-code\"", detail);
        Assert.Contains("Cấp lại mã", detail);
    }

    [Fact]
    public async Task Sensitive_outbox_body_is_wiped_after_sending()
    {
        var (shop, code) = await SetupAsync();

        EmailOutboxMessage? mail = null;
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            using var scope = _factory.Services.CreateScope();
            mail = await scope.ServiceProvider.GetRequiredService<AppDbContext>().EmailOutboxMessages.AsNoTracking()
                .SingleAsync(m => m.ToAddress == shop.Dealer.Email && m.IsSensitive);
            if (mail.Status == EmailOutboxStatus.Sent) break;
            await Task.Delay(250);
        }

        Assert.Equal(EmailOutboxStatus.Sent, mail!.Status);
        Assert.Equal(EmailOutboxRedaction.Placeholder, mail.HtmlBody);
        Assert.DoesNotContain(code, mail.HtmlBody);
    }

    [Fact]
    public async Task Sales_staff_cannot_issue_code_and_sees_no_button()
    {
        var dealer = await DealerTestData.DealerAsync(_factory);
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesStaff, allowRedirect: false);

        var detail = WebUtility.HtmlDecode(await client.GetStringAsync($"/admin/Dealers/Detail/{dealer.Id}"));
        Assert.Contains("Chưa cấp", detail);
        Assert.DoesNotContain("data-action=\"issue-code\"", detail);

        var token = await DealerTestData.TokenAsync(client, "/admin/Dealers");
        using var request = DealerTestData.Fetch(HttpMethod.Post, "/admin/Dealers/IssueCode",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["id"] = dealer.Id.ToString() }));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
        Assert.Null((await DealerAsync(dealer.Id)).AccessCodeHash);
    }

    [Fact]
    public async Task Wrong_unknown_and_pending_codes_get_the_same_message()
    {
        var pending = await DealerTestData.DealerAsync(_factory, DealerStatus.Pending);
        var pendingCode = DealerAccessCode.Generate();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.Dealers.SingleAsync(d => d.Id == pending.Id);
            row.AccessCodeHash = scope.ServiceProvider.GetRequiredService<IDealerAccessCodeHasher>().Hash(pendingCode);
            row.AccessCodeIssuedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var guest = _factory.CreateClient();
        var token = await GuestTokenAsync(guest);
        foreach (var attempt in new[] { "abc", DealerAccessCode.Format(DealerAccessCode.Generate()), DealerAccessCode.Format(pendingCode) })
        {
            var json = await EnterCodeAsync(guest, token, attempt);
            Assert.False(json.GetProperty("ok").GetBoolean());
            Assert.Equal(Invalid, json.GetProperty("error").GetString());
        }

        var cart = WebUtility.HtmlDecode(await guest.GetStringAsync("/cart"));
        Assert.DoesNotContain("data-dealer-active", cart);
    }

    [Fact]
    public async Task Company_dealer_enters_code_gets_tiered_prices_prefill_and_places_order()
    {
        var (shop, code) = await SetupAsync();
        var guest = _factory.CreateClient();
        var token = await GuestTokenAsync(guest);
        await AddToCartAsync(guest, token, shop.CoralVariant, 15);
        await AddToCartAsync(guest, token, shop.MauVariant, 10);

        var guestCart = WebUtility.HtmlDecode(await guest.GetStringAsync("/cart"));
        Assert.DoesNotContain("data-line-discount", guestCart);
        Assert.Contains("data-dealer-code-form", guestCart);

        var entered = await EnterCodeAsync(guest, token, code.ToLowerInvariant().Replace("-", " "));
        Assert.True(entered.GetProperty("ok").GetBoolean(), entered.ToString());

        var cart = WebUtility.HtmlDecode(await guest.GetStringAsync("/cart"));
        Assert.Contains("data-dealer-active", cart);
        Assert.Contains(shop.Dealer.BusinessName, cart);
        Assert.Contains("data-line-percent=\"25\"", cart);
        Assert.Contains("data-line-percent=\"22\"", cart);
        Assert.Contains("-595.000đ", cart);

        var checkout = WebUtility.HtmlDecode(await guest.GetStringAsync("/checkout"));
        Assert.Contains("value=\"Lê Liên Hệ\"", checkout);
        Assert.Contains($"value=\"{shop.Dealer.Email}\"", checkout);
        Assert.Contains("data-dealer-invoice", checkout);
        Assert.Contains(shop.Dealer.TaxCode, checkout);
        Assert.Contains("Không bắt buộc với đại lý doanh nghiệp.", checkout);
        Assert.DoesNotContain(code, checkout);

        var customerEmail = $"buyer-{DealerTestData.Suffix()}@test.local";
        var draft = await guest.PostAsync("/checkout", new FormUrlEncodedContent(DraftFields(token, customerEmail)));
        Assert.EndsWith("/checkout/confirm", draft.RequestMessage!.RequestUri!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        var confirm = WebUtility.HtmlDecode(await draft.Content.ReadAsStringAsync());
        Assert.Contains("data-confirm-discount", confirm);
        Assert.Contains("-595.000đ", confirm);

        var submit = await guest.PostAsync("/checkout/submit", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        var placed = submit.RequestMessage!.RequestUri!.AbsolutePath.Contains("/checkout/success/", StringComparison.OrdinalIgnoreCase);
        Assert.True(placed, ErrorOn(await submit.Content.ReadAsStringAsync()));
        var orderNumber = submit.RequestMessage.RequestUri.AbsolutePath.Split('/').Last();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.Include(o => o.Items).AsNoTracking().SingleAsync(o => o.OrderNumber == orderNumber);
        Assert.Equal(shop.Dealer.Id, order.DealerId);
        Assert.Equal(OrderSource.Website, order.Source);
        Assert.Equal(595_000m, order.DiscountAmount);
        Assert.Equal(2_500_000m, order.SubTotal);
        Assert.Equal(2_500_000m - 595_000m + order.ShippingFee, order.Total);
        Assert.True(string.IsNullOrEmpty(order.CitizenId));
        var coral = order.Items.Single(i => i.Sku == shop.CoralSku);
        Assert.Equal((25m, 375_000m, DealerDiscountSource.Default), (coral.DiscountPercent, coral.DiscountAmount, coral.DiscountSource));
        Assert.Equal(22m, order.Items.Single(i => i.Sku == shop.MauSku).DiscountPercent);
        Assert.Equal(shop.Dealer.BusinessName, order.InvoiceBuyerName);
        Assert.Equal(shop.Dealer.TaxCode, order.InvoiceTaxCode);
        Assert.Equal(shop.Dealer.InvoiceEmail, order.InvoiceEmail);

        var mails = await db.EmailOutboxMessages.AsNoTracking().Where(m => !m.IsSensitive).ToListAsync();
        Assert.Contains(mails, m => m.ToAddress == customerEmail && (m.Subject + m.HtmlBody).Contains(orderNumber));
        Assert.Contains(mails, m => m.ToAddress == shop.Dealer.Email && (m.Subject + m.HtmlBody).Contains(orderNumber));

        var after = WebUtility.HtmlDecode(await guest.GetStringAsync("/cart"));
        Assert.DoesNotContain("data-dealer-active", after);

        var admin = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesManager);
        var list = WebUtility.HtmlDecode(await admin.GetStringAsync($"/admin/Orders?q={orderNumber}"));
        Assert.Contains("data-dealer-badge", list);
    }

    [Fact]
    public async Task Household_dealer_still_needs_citizen_id()
    {
        var (shop, code) = await SetupAsync(DealerBusinessType.Household);
        var guest = _factory.CreateClient();
        var token = await GuestTokenAsync(guest);
        await AddToCartAsync(guest, token, shop.CoralVariant, 1);
        Assert.True((await EnterCodeAsync(guest, token, code)).GetProperty("ok").GetBoolean());

        var checkout = WebUtility.HtmlDecode(await guest.GetStringAsync("/checkout"));
        Assert.Contains("value=\"001234567890\"", checkout);

        var response = await guest.PostAsync("/checkout", new FormUrlEncodedContent(DraftFields(token, shop.Dealer.Email)));
        Assert.Contains("CCCD phải gồm 9 hoặc 12 chữ số.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Reissue_invalidates_old_code_and_open_session_before_submit()
    {
        var (shop, oldCode) = await SetupAsync();
        var guest = _factory.CreateClient();
        var token = await GuestTokenAsync(guest);
        await AddToCartAsync(guest, token, shop.CoralVariant, 15);
        Assert.True((await EnterCodeAsync(guest, token, oldCode)).GetProperty("ok").GetBoolean());
        var customerEmail = $"reissue-{DealerTestData.Suffix()}@test.local";
        await guest.PostAsync("/checkout", new FormUrlEncodedContent(DraftFields(token, customerEmail)));

        var admin = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesManager);
        var adminToken = await DealerTestData.TokenAsync(admin, "/admin/Dealers");
        var json = await DealerTestData.JsonAsync(await admin.PostAsync("/admin/Dealers/IssueCode",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = adminToken, ["id"] = shop.Dealer.Id.ToString() })));
        Assert.True(json.GetProperty("ok").GetBoolean(), json.ToString());
        Assert.Equal("Đã cấp mã đại lý mới. Mã cũ không còn hiệu lực.", json.GetProperty("message").GetString());
        var newCode = _factory.LastCodeFor(shop.Dealer.Email);
        Assert.NotEqual(oldCode, newCode);

        var submit = await guest.PostAsync("/checkout/submit", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.EndsWith("/checkout/confirm", submit.RequestMessage!.RequestUri!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Expired, WebUtility.HtmlDecode(await submit.Content.ReadAsStringAsync()));
        using (var scope = _factory.Services.CreateScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders.AnyAsync(o => o.CustomerEmail == customerEmail));

        var old = await EnterCodeAsync(guest, token, oldCode);
        Assert.Equal(Invalid, old.GetProperty("error").GetString());
        Assert.True((await EnterCodeAsync(guest, token, newCode)).GetProperty("ok").GetBoolean());

        using (var scope = _factory.Services.CreateScope())
        {
            var audits = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.AsNoTracking()
                .Where(a => a.Action == "Dealer.CodeIssued" && a.EntityId == shop.Dealer.Id.ToString()).ToListAsync();
            Assert.Equal(2, audits.Count);
        }
    }

    [Fact]
    public async Task Removing_code_returns_to_guest_prices()
    {
        var (shop, code) = await SetupAsync();
        var guest = _factory.CreateClient();
        var token = await GuestTokenAsync(guest);
        await AddToCartAsync(guest, token, shop.CoralVariant, 15);
        Assert.True((await EnterCodeAsync(guest, token, code)).GetProperty("ok").GetBoolean());

        await guest.PostAsync("/checkout/dealer-code/remove", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["returnUrl"] = "/cart",
        }));

        var cart = WebUtility.HtmlDecode(await guest.GetStringAsync("/cart"));
        Assert.DoesNotContain("data-dealer-active", cart);
        Assert.DoesNotContain("data-line-discount", cart);
    }
}

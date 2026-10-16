using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Application.DealerDiscounts;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Tests;

/// <summary>Manual dealer orders: tiered per-line discount, preview endpoint, manual override RBAC, invoice snapshot. Own fixture.</summary>
public class DealerOrderDiscountIntegrationTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public DealerOrderDiscountIntegrationTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    private sealed record Setup(Dealer Dealer, int GroupId, int Coral, string CoralSku, int Mau, string MauSku);

    /// <summary>KIRARA báo giá: Coral 20/25/30, Màu 18/22/27; giá 100.000đ.</summary>
    private async Task<Setup> SetupAsync(DealerBusinessType type = DealerBusinessType.Company)
    {
        var (coral, coralSku) = await DealerTestData.ProductAsync(_factory, "Coral", 100_000m);
        var (mau, mauSku) = await DealerTestData.ProductAsync(_factory, "Màu", 100_000m);
        var groupId = await DealerTestData.GroupAsync(_factory, "KIRARA " + DealerTestData.Suffix(), [coral, mau],
            [20m, 18m], [25m, 22m], [30m, 27m]);
        var dealer = await DealerTestData.DealerAsync(_factory, type: type);
        return new Setup(dealer, groupId, coral, coralSku, mau, mauSku);
    }

    private Dictionary<string, string> OrderFields(string token, Setup s, string citizenId = "")
    {
        var (province, commune) = DealerTestData.Hanoi(_factory);
        return new()
        {
            ["__RequestVerificationToken"] = token,
            ["Source"] = "Store",
            ["Status"] = "Confirmed",
            ["CustomerName"] = "Lê Liên Hệ",
            ["CustomerPhone"] = "0912345678",
            ["CitizenId"] = citizenId,
            ["DealerId"] = s.Dealer.Id.ToString(),
            ["ShippingAddress"] = "12 Hàng Bài",
            ["ShippingProvinceCode"] = province,
            ["ShippingCommuneCode"] = commune,
            ["Lines[0].Sku"] = s.CoralSku,
            ["Lines[0].Quantity"] = "15",
            ["Lines[1].Sku"] = s.MauSku,
            ["Lines[1].Quantity"] = "10",
        };
    }

    private async Task<Order> OrderAsync(string orderNumber)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders
            .Include(o => o.Items).AsNoTracking().SingleAsync(o => o.OrderNumber == orderNumber);
    }

    [Fact]
    public async Task Preview_sums_quantity_per_group_and_flags_unconfigured_products()
    {
        var s = await SetupAsync();
        var (_, lonelySku) = await DealerTestData.ProductAsync(_factory, "Lẻ", 50_000m);
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesStaff);

        var response = await client.PostAsJsonAsync("/admin/orders/dealer-discount-preview", new
        {
            dealerId = s.Dealer.Id,
            lines = new object[]
            {
                new { sku = s.CoralSku, quantity = 15 },
                new { sku = s.MauSku, quantity = 10, discountPercent = 50 },
                new { sku = lonelySku, quantity = 2 },
                new { sku = "KHONG-CO", quantity = 1 },
            }
        });
        var json = await DealerTestData.JsonAsync(response);
        var lines = json.GetProperty("lines");

        Assert.Equal(25m, lines[0].GetProperty("percent").GetDecimal());
        Assert.Equal(375_000m, lines[0].GetProperty("amount").GetDecimal());
        Assert.EndsWith(": 20-50", lines[0].GetProperty("tierLabel").GetString());
        Assert.Equal(22m, lines[1].GetProperty("percent").GetDecimal());
        Assert.False(lines[1].GetProperty("manual").GetBoolean());
        Assert.True(lines[2].GetProperty("unconfigured").GetBoolean());
        Assert.Equal(0m, lines[2].GetProperty("percent").GetDecimal());
        Assert.Contains("KHONG-CO", lines[3].GetProperty("error").GetString());
        Assert.Equal(595_000m, json.GetProperty("discount").GetDecimal());
        Assert.Equal(2_600_000m, json.GetProperty("subTotal").GetDecimal());
    }

    [Fact]
    public async Task Sales_staff_order_ignores_manual_percent_and_snapshots_invoice_without_citizen_id()
    {
        var s = await SetupAsync();
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesStaff);
        var form = WebUtility.HtmlDecode(await client.GetStringAsync("/admin/Orders/Create"));
        Assert.Contains($"data-invoice-taxcode=\"{s.Dealer.TaxCode}\"", form);
        Assert.DoesNotContain("js-line-manual", form);
        Assert.DoesNotContain("DealerDiscountPercent", form);
        var fields = OrderFields(AdminLoginHelper.ExtractRequestVerificationToken(form)!, s);
        fields["Lines[0].DiscountPercent"] = "50";

        var json = await DealerTestData.JsonAsync(await client.PostAsync("/admin/Orders/Create", new FormUrlEncodedContent(fields)));
        Assert.True(json.GetProperty("ok").GetBoolean(), json.ToString());

        var order = await OrderAsync(json.GetProperty("orderNumber").GetString()!);
        var coral = order.Items.Single(i => i.Sku == s.CoralSku);
        Assert.Equal((25m, 375_000m, DealerDiscountSource.Default), (coral.DiscountPercent, coral.DiscountAmount, coral.DiscountSource));
        Assert.Equal(22m, order.Items.Single(i => i.Sku == s.MauSku).DiscountPercent);
        Assert.Equal(595_000m, order.DiscountAmount);
        Assert.Equal(2_500_000m - 595_000m, order.Total);
        Assert.Null(order.DealerDiscountPercent);
        Assert.Equal(s.Dealer.BusinessName, order.InvoiceBuyerName);
        Assert.Equal(s.Dealer.TaxCode, order.InvoiceTaxCode);
        Assert.Equal(s.Dealer.InvoiceEmail, order.InvoiceEmail);
        Assert.Contains("12 Hàng Bài", order.InvoiceAddress);
    }

    [Fact]
    public async Task Sales_manager_manual_percent_requires_internal_note_then_is_saved_as_manual()
    {
        var s = await SetupAsync();
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesManager);
        var form = await client.GetStringAsync("/admin/Orders/Create");
        Assert.Contains("js-line-manual", form);
        var fields = OrderFields(AdminLoginHelper.ExtractRequestVerificationToken(form)!, s);
        fields["Lines[0].DiscountPercent"] = "35.5";

        var rejected = await client.PostAsync("/admin/Orders/Create", new FormUrlEncodedContent(fields));
        Assert.StartsWith("text/html", rejected.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Vui lòng ghi chú nội bộ lý do sửa tay % chiết khấu.", WebUtility.HtmlDecode(await rejected.Content.ReadAsStringAsync()));

        fields["InternalNotes"] = "Khách thân thiết, GĐ duyệt";
        var json = await DealerTestData.JsonAsync(await client.PostAsync("/admin/Orders/Create", new FormUrlEncodedContent(fields)));
        Assert.True(json.GetProperty("ok").GetBoolean(), json.ToString());

        var order = await OrderAsync(json.GetProperty("orderNumber").GetString()!);
        var coral = order.Items.Single(i => i.Sku == s.CoralSku);
        Assert.Equal(35.5m, coral.DiscountPercent);
        Assert.Equal(DealerDiscountSource.Manual, coral.DiscountSource);
        Assert.Equal(532_500m, coral.DiscountAmount);

        using var scope = _factory.Services.CreateScope();
        var audit = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs
            .SingleAsync(a => a.Action == "Order.ManualCreated" && a.EntityId == order.Id.ToString());
        Assert.Contains("ManualDiscounts", audit.NewValues);
        Assert.Contains(s.CoralSku, audit.NewValues);
    }

    [Fact]
    public async Task Editing_default_table_after_order_does_not_change_snapshot_and_views_show_discount()
    {
        var s = await SetupAsync();
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesManager);
        var token = await DealerTestData.TokenAsync(client, "/admin/Orders/Create");
        var json = await DealerTestData.JsonAsync(await client.PostAsync("/admin/Orders/Create", new FormUrlEncodedContent(OrderFields(token, s))));
        var order = await OrderAsync(json.GetProperty("orderNumber").GetString()!);

        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IAdminDealerDiscountService>();
            var edit = (await service.GetGroupEditAsync(s.GroupId)).Input;
            foreach (var t in edit.Tiers)
                foreach (var pid in t.Percents.Keys.ToList())
                    t.Percents[pid] = 1;
            Assert.True((await service.SaveGroupAsync(edit, null)).Ok);
        }

        var after = await OrderAsync(order.OrderNumber);
        Assert.Equal(25m, after.Items.Single(i => i.Sku == s.CoralSku).DiscountPercent);
        Assert.Equal(595_000m, after.DiscountAmount);

        var detail = WebUtility.HtmlDecode(await client.GetStringAsync($"/admin/Orders/Detail?id={order.Id}"));
        Assert.Contains("data-order-invoice", detail);
        Assert.Contains(s.Dealer.TaxCode, detail);
        Assert.Contains("CK 25%", detail);

        var print = WebUtility.HtmlDecode(await client.GetStringAsync($"/admin/Orders/Print/{order.Id}"));
        Assert.Contains("data-print-invoice", print);
        Assert.Contains("CK %", print);
        Assert.Contains("25%", print);
    }

    [Fact]
    public async Task Household_dealer_order_still_requires_citizen_id()
    {
        var s = await SetupAsync(DealerBusinessType.Household);
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesStaff);
        var token = await DealerTestData.TokenAsync(client, "/admin/Orders/Create");

        var response = await client.PostAsync("/admin/Orders/Create", new FormUrlEncodedContent(OrderFields(token, s)));

        Assert.StartsWith("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("CCCD phải gồm 9 hoặc 12 chữ số.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }
}

using NewHarian.Application.Abstractions;
using NewHarian.Domain.Enums;
using NewHarian.E2E.Fixtures;
using NewHarian.E2E.Pages;

namespace NewHarian.E2E.Journeys;

[Collection(E2ECollection.Name)]
[Trait("Category", "E2E")]
[Trait("Priority", "P0")]
public sealed class DealerDiscountJourneyTests(E2EFixture fx)
{
    /// <summary>Báo giá KIRARA: 15 Coral + 10 Màu = 25 cùng nhóm, nên cả hai dòng rơi vào mức 20-50.</summary>
    [E2EFact]
    public async Task Sales_manager_builds_tiered_table_and_dealer_order_gets_per_line_discount()
    {
        var coral = await E2EDealerData.ProductAsync(fx.Services, "E2E Coral");
        var mau = await E2EDealerData.ProductAsync(fx.Services, "E2E Màu");
        var dealer = await E2EDealerData.ApprovedCompanyAsync(fx.Services);
        var groupName = $"E2E KIRARA {E2EDealerData.Suffix()}";
        var orderNo = "";

        await using (var manager = await fx.NewRoleContextAsync(AppRoles.SalesManager))
        {
            await fx.RunAsync(manager, "Sales_manager_builds_tiered_table", async page =>
            {
                var discounts = new AdminDealerDiscountsPage(page);
                await discounts.GotoAsync();
                await discounts.CreateGroupAsync(groupName, [19, 50],
                    (coral.Name, ["20", "25", "30"]),
                    (mau.Name, ["18", "22.5", "27"]));
                await discounts.Toasts.ExpectSuccessAsync("Đã thêm nhóm chiết khấu.");
                await Expect(discounts.Group(groupName)).ToContainTextAsync("20-50");
                await Expect(discounts.Group(groupName)).ToContainTextAsync("Từ 51");

                var form = new AdminManualOrderForm(page);
                await form.OpenAsync();
                await form.SelectDealerAsync(dealer.OptionLabel, E2EDealerData.CommuneCode);
                await Expect(form.Invoice).ToContainTextAsync(dealer.TaxCode);
                await Expect(form.CitizenRequiredMark).ToBeHiddenAsync();
                await form.SetLineAsync(0, coral.Sku, 15);
                await form.SetLineAsync(1, mau.Sku, 10);
                await Expect(form.LinePrice(0)).ToHaveValueAsync("100.000 VNĐ");
                await form.SetUnitPriceAsync(0, "1500000");
                await Expect(form.LinePrice(0)).ToHaveValueAsync("1.500.000 VNĐ");
                await form.SetUnitPriceAsync(0, "100000");
                await Expect(form.LinePrice(0)).ToHaveValueAsync("100.000 VNĐ");

                await Expect(form.LineDiscount(0)).ToContainTextAsync(": 20-50");
                await Expect(form.LineDiscount(0)).ToContainTextAsync("CK 25%");
                await Expect(form.LineDiscount(1)).ToContainTextAsync("CK 22.5%");
                await Expect(form.Summary).ToContainTextAsync("-600.000 VNĐ");
                await Expect(form.ManualPercentInputs.First).ToBeVisibleAsync();

                orderNo = await form.SubmitAsync();

                var orders = new AdminOrdersPage(page);
                await orders.GotoAsync(orderNo);
                await orders.OpenDetailAsync(orderNo);
                await Expect(orders.Modal.Locator("[data-order-invoice]")).ToContainTextAsync(dealer.TaxCode);
                await Expect(orders.Modal.Locator("[data-line-discount]").First).ToContainTextAsync("CK 25%");
            });
        }

        var items = await E2EDealerData.OrderItemsAsync(fx.Services, orderNo);
        Assert.Equal((25m, 375_000m, DealerDiscountSource.Default),
            (items[0].DiscountPercent, items[0].DiscountAmount, items[0].DiscountSource));
        Assert.Equal((22.5m, 225_000m), (items[1].DiscountPercent, items[1].DiscountAmount));

        await using var staff = await fx.NewRoleContextAsync(AppRoles.SalesStaff);
        await fx.RunAsync(staff, "Sales_staff_reads_table_and_cannot_override", async page =>
        {
            var discounts = new AdminDealerDiscountsPage(page);
            await discounts.GotoAsync();
            await Expect(discounts.Group(groupName)).ToBeVisibleAsync();
            await Expect(discounts.AddButton).ToHaveCountAsync(0);

            var form = new AdminManualOrderForm(page);
            await form.OpenAsync();
            await form.SelectDealerAsync(dealer.OptionLabel, E2EDealerData.CommuneCode);
            await form.SetLineAsync(0, coral.Sku, 51);
            await Expect(form.LineDiscount(0)).ToContainTextAsync("CK 30%");
            await Expect(form.ManualPercentInputs).ToHaveCountAsync(0);
        });
    }

    [E2EFact]
    public async Task Sales_manager_sets_dealer_override_and_next_order_uses_it()
    {
        var coral = await E2EDealerData.ProductAsync(fx.Services, "E2E Coral");
        var mau = await E2EDealerData.ProductAsync(fx.Services, "E2E Màu");
        var dealer = await E2EDealerData.ApprovedCompanyAsync(fx.Services);
        await E2EDealerData.KiraraGroupAsync(fx.Services, $"E2E KIRARA {E2EDealerData.Suffix()}", coral, mau);
        var orderNo = "";

        await using var manager = await fx.NewRoleContextAsync(AppRoles.SalesManager);
        await fx.RunAsync(manager, nameof(Sales_manager_sets_dealer_override_and_next_order_uses_it), async page =>
        {
            var dealers = new AdminDealersPage(page);
            await dealers.SearchAsync(dealer.TaxCode);
            await dealers.OpenDetailAsync(dealer.TaxCode);
            await dealers.Action("edit-overrides").ClickAsync();
            await Expect(dealers.Modal.Locator("#dealer-overrides-form")).ToBeVisibleAsync();
            await Expect(dealers.OverrideInput(coral.Name, "20-50")).ToHaveAttributeAsync("placeholder", "25");
            await dealers.OverrideInput(coral.Name, "20-50").FillAsync("28");
            await dealers.Modal.GetByRole(AriaRole.Button, new() { Name = "Lưu", Exact = true }).ClickAsync();

            await dealers.Toasts.ExpectSuccessAsync("Đã cập nhật % riêng.");
            await Expect(dealers.Row(dealer.TaxCode).Locator("[data-has-overrides]")).ToBeVisibleAsync();

            var form = new AdminManualOrderForm(page);
            await form.OpenAsync();
            await form.SelectDealerAsync(dealer.OptionLabel, E2EDealerData.CommuneCode);
            await form.SetLineAsync(0, coral.Sku, 15);
            await form.SetLineAsync(1, mau.Sku, 10);
            await Expect(form.LineDiscount(0)).ToContainTextAsync("CK 28%");
            await Expect(form.LineDiscount(0)).ToContainTextAsync("Riêng đại lý");
            await Expect(form.LineDiscount(1)).ToContainTextAsync("CK 22%");
            orderNo = await form.SubmitAsync();
        });

        var items = await E2EDealerData.OrderItemsAsync(fx.Services, orderNo);
        Assert.Equal((28m, DealerDiscountSource.DealerOverride), (items[0].DiscountPercent, items[0].DiscountSource));
        Assert.Equal((22m, DealerDiscountSource.Default), (items[1].DiscountPercent, items[1].DiscountSource));
    }
}

using NewHarian.Application.Abstractions;
using NewHarian.E2E.Fixtures;
using NewHarian.E2E.Pages;

namespace NewHarian.E2E.Journeys;

[Collection(E2ECollection.Name)]
[Trait("Category", "E2E")]
[Trait("Priority", "P0")]
public sealed class AdminOrderJourneyTests(E2EFixture fx)
{
    [E2EFact]
    public async Task Sales_manager_takes_cod_order_from_confirmation_to_delivered_with_fefo_deduction()
    {
        var orderNo = await PlaceGuestCodOrderAsync(nameof(Sales_manager_takes_cod_order_from_confirmation_to_delivered_with_fefo_deduction));

        await using var context = await fx.NewRoleContextAsync(AppRoles.SalesManager);
        await fx.RunAsync(context, nameof(Sales_manager_takes_cod_order_from_confirmation_to_delivered_with_fefo_deduction), async page =>
        {
            var orders = new AdminOrdersPage(page);
            await orders.GotoAsync(orderNo);
            await orders.ExpectStatusAsync(orderNo, "AwaitingConfirmation");
            await orders.OpenDetailAsync(orderNo);
            await Expect(orders.ActionButton("Hủy đơn")).ToBeVisibleAsync();

            await orders.RunActionAsync(orderNo, "Xác nhận COD", "Confirmed");
            await Expect(orders.HistoryMessages).ToContainTextAsync(["Tiếp nhận đơn (xác nhận COD)"]);
            // FEFO preview skips the expired lot even though its HSD is earlier.
            await Expect(orders.StockPick).ToContainTextAsync("Gợi ý lấy hàng (FEFO)");
            await Expect(orders.StockPick).ToContainTextAsync($"lô {E2ETestData.ValidLotCode}");
            await Expect(orders.StockPick).Not.ToContainTextAsync(E2ETestData.ExpiredLotCode);

            await orders.RunActionAsync(orderNo, "Bắt đầu xử lý", "Processing");
            await Expect(orders.StockPick).ToContainTextAsync("Đã lấy hàng (FEFO)");
            await Expect(orders.StockPick).ToContainTextAsync($"lô {E2ETestData.ValidLotCode}");
            await Expect(orders.StockPick).ToContainTextAsync("lấy 1");
            await Expect(orders.StockPick).Not.ToContainTextAsync("thiếu");

            await orders.RunActionAsync(orderNo, "Đã giao vận", "Shipped");
            await orders.RunActionAsync(orderNo, "Hoàn thành", "Delivered");

            await Expect(orders.HistoryMessages).ToContainTextAsync(
            [
                "Tiếp nhận đơn (xác nhận COD)",
                "Bắt đầu xử lý / đóng gói",
                "Gửi đơn đi (giao vận)",
                "Hoàn thành giao hàng"
            ]);
            await Expect(orders.Modal.Locator(".order-actions button")).ToHaveCountAsync(0);
        });
    }

    [E2EFact]
    public async Task Sales_staff_can_confirm_cod_but_does_not_see_cancel_button()
    {
        var orderNo = await PlaceGuestCodOrderAsync(nameof(Sales_staff_can_confirm_cod_but_does_not_see_cancel_button));

        await using var context = await fx.NewRoleContextAsync(AppRoles.SalesStaff);
        await fx.RunAsync(context, nameof(Sales_staff_can_confirm_cod_but_does_not_see_cancel_button), async page =>
        {
            var orders = new AdminOrdersPage(page);
            await orders.GotoAsync(orderNo);
            await orders.OpenDetailAsync(orderNo);

            await Expect(orders.ActionButton("Xác nhận COD")).ToBeVisibleAsync();
            await Expect(orders.ActionButton("Hủy đơn")).ToHaveCountAsync(0);
            // Sales Staff lacks Orders.PreviewStockPick: no FEFO preview before stock is deducted.
            await Expect(orders.StockPick).ToHaveCountAsync(0);

            await orders.RunActionAsync(orderNo, "Xác nhận COD", "Confirmed");
            await Expect(orders.ActionButton("Bắt đầu xử lý")).ToBeVisibleAsync();
            await Expect(orders.ActionButton("Hủy đơn")).ToHaveCountAsync(0);
            await Expect(orders.StockPick).ToHaveCountAsync(0);
        });
    }

    private Task<string> PlaceGuestCodOrderAsync(string testName)
        => GuestOrders.PlaceAsync(fx, testName, GuestCustomer.Unique(), GuestPayment.Cod, E2ETestData.SkuSmall, quantity: 1);
}

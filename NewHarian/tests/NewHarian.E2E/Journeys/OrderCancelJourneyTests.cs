using System.Text.RegularExpressions;
using NewHarian.Application.Abstractions;
using NewHarian.Domain.Enums;
using NewHarian.E2E.Fixtures;
using NewHarian.E2E.Pages;

namespace NewHarian.E2E.Journeys;

[Collection(E2ECollection.Name)]
[Trait("Category", "E2E")]
[Trait("Priority", "P1")]
public sealed class OrderCancelJourneyTests(E2EFixture fx)
{
    [E2EFact]
    public async Task Sales_manager_cancels_processing_order_and_stock_is_restored()
    {
        const string name = nameof(Sales_manager_cancels_processing_order_and_stock_is_restored);
        const int qty = 3;
        var orderNo = await GuestOrders.PlaceAsync(fx, name, GuestCustomer.Unique(), GuestPayment.Cod, E2ETestData.SkuSmall, qty);

        await using var context = await fx.NewRoleContextAsync(AppRoles.SalesManager);
        await fx.RunAsync(context, name, async page =>
        {
            var orders = new AdminOrdersPage(page);
            await orders.GotoAsync(orderNo);
            await orders.OpenDetailAsync(orderNo);
            await orders.RunActionAsync(orderNo, "Xác nhận COD", "Confirmed");

            // The E2E collection runs sequentially, so no other journey moves this lot in between.
            var lotBefore = await LotQuantityAsync();
            await orders.RunActionAsync(orderNo, "Bắt đầu xử lý", "Processing");
            await Expect(orders.StockPick).ToContainTextAsync("Đã lấy hàng (FEFO)");
            await Expect(orders.StockPick).ToContainTextAsync($"lấy {qty}");
            Assert.Equal(lotBefore - qty, await LotQuantityAsync());
            Assert.Equal(-qty, await E2ETestData.MovementQuantityAsync(fx.Services, orderNo, StockMovementType.Issue));

            await orders.RunActionAsync(orderNo, "Hủy đơn", "Cancelled");
            await Expect(orders.HistoryMessages).ToContainTextAsync(
            [
                "Tiếp nhận đơn (xác nhận COD)",
                "Bắt đầu xử lý / đóng gói",
                "Hủy đơn"
            ]);
            await Expect(orders.Modal.Locator(".order-actions button")).ToHaveCountAsync(0);
            // No picking for a cancelled order, even with Orders.PreviewStockPick.
            await Expect(orders.StockPick).ToHaveCountAsync(0);
            Assert.Equal(lotBefore, await LotQuantityAsync());
            Assert.Equal(qty, await E2ETestData.MovementQuantityAsync(fx.Services, orderNo, StockMovementType.Restore));

            var history = new AdminInventoryHistoryPage(page);
            await history.GotoAsync();
            var restoreRow = history.Rows.Filter(new()
            {
                HasTextRegex = new Regex($@"Hoàn kho do hủy đơn hàng {Regex.Escape(orderNo)}\b")
            });
            await Expect(restoreRow).ToHaveCountAsync(1);
            await Expect(restoreRow.Locator("td").First).ToHaveTextAsync("Hoàn kho");
            await Expect(restoreRow).ToContainTextAsync($"+{qty}");
            await Expect(restoreRow).ToContainTextAsync(E2ETestData.ProductName);
        });

        Task<int> LotQuantityAsync()
            => E2ETestData.LotQuantityAsync(fx.Services, E2ETestData.SkuSmall, E2ETestData.ValidLotCode);
    }

    [E2EFact]
    public async Task Guest_cancels_unpaid_bank_transfer_order_and_staff_sees_cancellation()
    {
        const string name = nameof(Guest_cancels_unpaid_bank_transfer_order_and_staff_sees_cancellation);
        var customer = GuestCustomer.Unique();
        var orderNo = "";

        await using (var guest = await fx.NewGuestContextAsync())
        {
            await fx.RunAsync(guest, name + "_guest", async page =>
            {
                orderNo = await GuestShop.PlaceOrderAsync(page, customer, GuestPayment.BankTransfer, E2ETestData.SkuSmall, quantity: 1);

                var track = new OrderTrackPage(page);
                await track.LookupAsync(orderNo, customer.Email);
                await Expect(track.Result).ToContainTextAsync("PendingPayment");
                await track.CancelAsync();

                await track.LookupAsync(orderNo, customer.Email);
                await Expect(track.Result).ToContainTextAsync("Cancelled");
                await Expect(track.CancelButton).ToHaveCountAsync(0);
            });
        }

        await using var context = await fx.NewRoleContextAsync(AppRoles.SalesStaff);
        await fx.RunAsync(context, name, async page =>
        {
            var orders = new AdminOrdersPage(page);
            await orders.GotoAsync(orderNo);
            await orders.ExpectStatusAsync(orderNo, "Cancelled");
            await orders.OpenDetailAsync(orderNo);
            await Expect(orders.HistoryMessages).ToContainTextAsync(["Đơn hàng được tạo", "Khách hủy đơn"]);
            await Expect(orders.Modal.Locator(".order-actions button")).ToHaveCountAsync(0);
            await Expect(orders.StockPick).ToHaveCountAsync(0);
        });
    }
}

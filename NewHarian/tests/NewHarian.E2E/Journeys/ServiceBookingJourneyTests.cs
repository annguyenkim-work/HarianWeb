using System.Text.RegularExpressions;
using NewHarian.Application.Abstractions;
using NewHarian.E2E.Fixtures;
using NewHarian.E2E.Pages;

namespace NewHarian.E2E.Journeys;

[Collection(E2ECollection.Name)]
[Trait("Category", "E2E")]
[Trait("Priority", "P1")]
public sealed class ServiceBookingJourneyTests(E2EFixture fx)
{
    [E2EFact]
    public async Task Guest_books_service_and_sales_staff_confirms_then_completes_it()
    {
        const string name = nameof(Guest_books_service_and_sales_staff_confirms_then_completes_it);
        var customer = GuestCustomer.Unique();
        var bookingNo = "";

        await using (var guest = await fx.NewGuestContextAsync())
        {
            await fx.RunAsync(guest, name + "_guest", async page =>
            {
                var booking = new ServiceBookingPage(page);
                await booking.OpenFromDetailAsync(E2ETestData.ServiceCategorySlug, E2ETestData.ServiceSlug, E2ETestData.ServiceName);
                bookingNo = await booking.SubmitAsync(
                    customer, E2ETestData.ServiceVariantLabel, DateOnly.FromDateTime(DateTime.Today.AddDays(3)));
            });
        }

        await using var context = await fx.NewRoleContextAsync(AppRoles.SalesStaff);
        await fx.RunAsync(context, name, async page =>
        {
            var bookings = new AdminServiceBookingsPage(page);
            await bookings.GotoAsync(bookingNo);
            await bookings.ExpectStatusAsync(bookingNo, "New");
            await Expect(bookings.Row(bookingNo)).ToContainTextAsync($"{E2ETestData.ServiceName} - {E2ETestData.ServiceVariantLabel}");
            await Expect(bookings.Row(bookingNo)).ToContainTextAsync(customer.Email);

            await bookings.OpenDetailAsync(bookingNo);
            await Expect(bookings.ActionButton("Hoàn thành")).ToHaveCountAsync(0);
            await bookings.RunActionAsync(bookingNo, "Xác nhận lịch", "Confirmed");

            // Completion needs CCCD (9/12 digits) + amount; the modal blocks it client-side first.
            var error = await bookings.ClickExpectingErrorToastAsync("Hoàn thành");
            Assert.Contains("CCCD", error);
            await bookings.ExpectStatusAsync(bookingNo, "Confirmed");

            await bookings.CitizenIdInput.FillAsync("001234567890");
            await bookings.AmountInput.FillAsync("350000");
            await bookings.RunActionAsync(bookingNo, "Hoàn thành", "Completed");

            await Expect(bookings.HistoryMessages).ToContainTextAsync(
            [
                "Đặt lịch được tạo",
                "Tiếp nhận lịch hẹn",
                "Hoàn thành dịch vụ"
            ]);
            await Expect(bookings.Modal).ToContainTextAsync("001234567890");
            await Expect(bookings.Modal).ToContainTextAsync(new Regex(@"350[.,]000đ"));
            await Expect(bookings.Modal.Locator(".order-actions button")).ToHaveCountAsync(0);
        });
    }

    [E2EFact]
    public async Task Sales_manager_bell_shows_new_booking_in_realtime()
    {
        const string name = nameof(Sales_manager_bell_shows_new_booking_in_realtime);
        await using var context = await fx.NewRoleContextAsync(AppRoles.SalesManager);
        await fx.RunAsync(context, name, async page =>
        {
            var shell = new AdminShell(page);

            // Baseline = initial unread count; then wait for the SignalR handshake frame before the guest books.
            var hubReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            page.WebSocket += (_, ws) =>
            {
                if (ws.Url.Contains("/admin/hubs/notifications", StringComparison.OrdinalIgnoreCase))
                    ws.FrameReceived += (_, _) => hubReady.TrySetResult();
            };
            var countResponse = await page.RunAndWaitForResponseAsync(
                () => page.GotoAsync("/admin"),
                r => r.Url.Contains("/admin/Notifications/UnreadCount", StringComparison.OrdinalIgnoreCase));
            Assert.True(countResponse.Ok);
            var before = (await countResponse.JsonAsync())!.Value.GetProperty("count").GetInt32();
            Assert.True(before < 99, $"Unread count {before} is capped in the badge; reset the E2E database.");
            await hubReady.Task.WaitAsync(TimeSpan.FromSeconds(15));

            var bookingNo = "";
            await using (var guest = await fx.NewGuestContextAsync())
            {
                await fx.RunAsync(guest, name + "_guest", async guestPage =>
                {
                    var booking = new ServiceBookingPage(guestPage);
                    await booking.OpenFromDetailAsync(E2ETestData.ServiceCategorySlug, E2ETestData.ServiceSlug, E2ETestData.ServiceName);
                    bookingNo = await booking.SubmitAsync(
                        GuestCustomer.Unique(), E2ETestData.ServiceVariantLabel, DateOnly.FromDateTime(DateTime.Today.AddDays(5)));
                });
            }

            // No reload: the badge only changes through the hub push (fallback polling is 45s, beyond the expect timeout).
            await Expect(shell.NotifyBadge).ToHaveTextAsync((before + 1).ToString());
            await shell.OpenNotificationsAsync();
            await Expect(shell.NotifyItems.Filter(new() { HasText = $"Đặt lịch mới {bookingNo}" })).ToHaveCountAsync(1);
        });
    }
}

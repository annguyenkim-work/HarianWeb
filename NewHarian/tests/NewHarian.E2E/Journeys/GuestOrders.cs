using NewHarian.E2E.Fixtures;
using NewHarian.E2E.Pages;

namespace NewHarian.E2E.Journeys;

/// <summary>Arrange step for admin journeys: a real guest checkout in its own traced context.</summary>
internal static class GuestOrders
{
    public static async Task<string> PlaceAsync(
        E2EFixture fx, string testName, GuestCustomer customer, GuestPayment payment, string sku, int quantity)
    {
        var orderNo = "";
        await using var guest = await fx.NewGuestContextAsync();
        await fx.RunAsync(guest, testName + "_guest", async page =>
        {
            orderNo = await GuestShop.PlaceOrderAsync(page, customer, payment, sku, quantity);
        });
        return orderNo;
    }
}

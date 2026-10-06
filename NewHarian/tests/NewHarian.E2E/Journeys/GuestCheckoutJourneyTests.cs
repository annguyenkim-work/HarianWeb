using System.Text.RegularExpressions;
using NewHarian.E2E.Fixtures;
using NewHarian.E2E.Pages;

namespace NewHarian.E2E.Journeys;

[Collection(E2ECollection.Name)]
[Trait("Category", "E2E")]
[Trait("Priority", "P0")]
public sealed class GuestCheckoutJourneyTests(E2EFixture fx)
{
    [E2EFact]
    public async Task Cod_checkout_shows_order_code_and_order_lookup_finds_it()
    {
        await using var context = await fx.NewGuestContextAsync();
        await fx.RunAsync(context, nameof(Cod_checkout_shows_order_code_and_order_lookup_finds_it), async page =>
        {
            var customer = GuestCustomer.Unique();

            var orderNo = await GuestShop.PlaceOrderAsync(page, customer, GuestPayment.Cod, E2ETestData.SkuLarge, quantity: 2);

            var success = new CheckoutSuccessPage(page);
            await Expect(success.BankBox).ToHaveCountAsync(0);
            await Expect(page.Locator("table.data-table")).ToContainTextAsync("× 2");

            var track = new OrderTrackPage(page);
            await track.LookupAsync(orderNo, customer.Email);
            await Expect(track.Result).ToContainTextAsync("AwaitingConfirmation");
            await Expect(track.Result).ToContainTextAsync("COD");
            await Expect(track.Result).ToContainTextAsync(customer.AddressLine);
        });
    }

    [E2EFact]
    public async Task Bank_transfer_checkout_shows_bank_info_and_vietqr()
    {
        await using var context = await fx.NewGuestContextAsync();
        await fx.RunAsync(context, nameof(Bank_transfer_checkout_shows_bank_info_and_vietqr), async page =>
        {
            var customer = GuestCustomer.Unique();

            var orderNo = await GuestShop.PlaceOrderAsync(page, customer, GuestPayment.BankTransfer, E2ETestData.SkuSmall, quantity: 1);

            var success = new CheckoutSuccessPage(page);
            await Expect(success.BankBox).ToBeVisibleAsync();
            await Expect(success.BankBox).ToContainTextAsync(E2ETestData.BankAccount);
            await Expect(success.BankBox).ToContainTextAsync(E2ETestData.BankAccountName);
            await Expect(success.BankBox).ToContainTextAsync(orderNo);
            await Expect(success.BankQrImage).ToBeVisibleAsync();
            await Expect(success.BankQrImage).ToHaveAttributeAsync("src", new Regex("^data:image/png;base64,"));

            var track = new OrderTrackPage(page);
            await track.LookupAsync(orderNo, customer.Email);
            await Expect(track.Result).ToContainTextAsync("PendingPayment");
            await Expect(track.Result.Locator("img.bank-qr-img")).ToBeVisibleAsync();
        });
    }
}

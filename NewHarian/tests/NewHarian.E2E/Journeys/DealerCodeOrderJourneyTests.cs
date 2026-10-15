using NewHarian.Application.Abstractions;
using NewHarian.Domain.Enums;
using NewHarian.E2E.Fixtures;
using NewHarian.E2E.Pages;

namespace NewHarian.E2E.Journeys;

[Collection(E2ECollection.Name)]
[Trait("Category", "E2E")]
[Trait("Priority", "P0")]
public sealed class DealerCodeOrderJourneyTests(E2EFixture fx)
{
    private const string Invalid = "Mã đại lý không hợp lệ.";

    /// <summary>15 Coral + 10 Màu = 25 cùng nhóm KIRARA: Coral 25%, Màu 22% (báo giá 18/22/27 ở E2EDealerData).</summary>
    [E2EFact]
    public async Task Dealer_enters_code_in_cart_and_places_discounted_website_order()
    {
        var coral = await E2EDealerData.ProductAsync(fx.Services, "E2E Coral");
        var mau = await E2EDealerData.ProductAsync(fx.Services, "E2E Màu");
        await E2EDealerData.KiraraGroupAsync(fx.Services, $"E2E KIRARA {E2EDealerData.Suffix()}", coral, mau);
        var dealer = await E2EDealerData.ApprovedCompanyAsync(fx.Services);
        var code = await E2EDealerData.KnownCodeAsync(fx.Services, dealer.Id);
        var orderNo = "";

        await using (var guest = await fx.NewGuestContextAsync())
        {
            await fx.RunAsync(guest, nameof(Dealer_enters_code_in_cart_and_places_discounted_website_order), async page =>
            {
                var detail = new ProductDetailPage(page);
                await detail.AddAsync(E2ETestData.CategorySlug, coral.Slug, coral.Name, 15);
                await detail.AddAsync(E2ETestData.CategorySlug, mau.Slug, mau.Name, 10);

                var cart = new CartPage(page);
                await cart.GotoAsync();
                await Expect(cart.LineDiscount(coral.Sku)).ToHaveCountAsync(0);

                await cart.DealerCode.EnterAsync("HRN-0000-0000-0000");
                await Expect(cart.DealerCode.Error).ToHaveTextAsync(Invalid);

                await cart.DealerCode.EnterAsync(code.ToLowerInvariant().Replace("-", " "));
                await Expect(cart.DealerCode.Active).ToContainTextAsync(dealer.BusinessName);
                await Expect(cart.LineDiscount(coral.Sku)).ToContainTextAsync("CK 25%");
                await Expect(cart.LineDiscount(mau.Sku)).ToContainTextAsync("CK 22%");
                await Expect(cart.Discount).ToHaveTextAsync("-595.000đ");

                await cart.ProceedToCheckoutAsync();
                var checkout = new CheckoutPage(page);
                await Expect(page.Locator("#CustomerName")).ToHaveValueAsync("Lê Liên Hệ");
                await Expect(page.Locator("#CitizenId")).Not.ToHaveAttributeAsync("required", "");
                await Expect(checkout.Invoice).ToContainTextAsync(dealer.TaxCode);
                await Expect(checkout.Summary).ToContainTextAsync("-595.000đ");
                await checkout.ContinueWithPrefillAsync(E2EDealerData.ProvinceCode, E2EDealerData.CommuneCode, GuestPayment.Cod);

                await Expect(page.Locator("[data-confirm-discount]")).ToHaveTextAsync("-595.000đ");
                await Expect(page.Locator("[data-dealer-invoice]")).ToContainTextAsync(dealer.BusinessName);
                await checkout.PlaceOrderAsync();

                var success = new CheckoutSuccessPage(page);
                orderNo = await success.OrderNumberAsync();
                await Expect(success.Discount).ToHaveTextAsync("-595.000đ");

                await detail.AddAsync(E2ETestData.CategorySlug, coral.Slug, coral.Name, 1);
                await cart.GotoAsync();
                await Expect(cart.DealerCode.Active).ToHaveCountAsync(0);
                await Expect(cart.LineDiscount(coral.Sku)).ToHaveCountAsync(0);
            });
        }

        var order = await E2EDealerData.OrderAsync(fx.Services, orderNo);
        Assert.Equal((dealer.Id, OrderSource.Website, 595_000m), (order.DealerId, order.Source, order.DiscountAmount));
        Assert.Equal(dealer.TaxCode, order.InvoiceTaxCode);
        var items = await E2EDealerData.OrderItemsAsync(fx.Services, orderNo);
        Assert.Contains(items, i => i.Sku == coral.Sku && i.DiscountPercent == 25m && i.DiscountSource == DealerDiscountSource.Default);
        Assert.Contains(items, i => i.Sku == mau.Sku && i.DiscountPercent == 22m);

        await using var staff = await fx.NewRoleContextAsync(AppRoles.SalesStaff);
        await fx.RunAsync(staff, "Sales_staff_sees_dealer_badge_on_website_order", async page =>
        {
            var orders = new AdminOrdersPage(page);
            await orders.GotoAsync(orderNo);
            await Expect(orders.Row(orderNo).Locator("[data-dealer-badge]")).ToHaveTextAsync("Đại lý");
            await orders.OpenDetailAsync(orderNo);
            await Expect(orders.Modal.Locator("[data-order-invoice]")).ToContainTextAsync(dealer.TaxCode);
        });
    }

    [E2EFact]
    public async Task Sales_manager_approves_dealer_and_code_is_issued_masked()
    {
        var dealer = await E2EDealerData.ApprovedCompanyAsync(fx.Services, DealerStatus.Pending);

        await using var manager = await fx.NewRoleContextAsync(AppRoles.SalesManager);
        await fx.RunAsync(manager, nameof(Sales_manager_approves_dealer_and_code_is_issued_masked), async page =>
        {
            var dealers = new AdminDealersPage(page);
            await dealers.SearchAsync(dealer.TaxCode);
            await dealers.OpenDetailAsync(dealer.TaxCode);
            await Expect(dealers.Action("issue-code")).ToHaveCountAsync(0);
            await dealers.Action("approve").ClickAsync();
            await dealers.Toasts.ExpectSuccessAsync("Đã duyệt đại lý và gửi mã đại lý qua email.");

            await dealers.OpenDetailAsync(dealer.TaxCode);
            await Expect(dealers.Modal.Locator("[data-field=accessCode]")).ToContainTextAsync("HRN-••••-••••-");
            await Expect(dealers.Action("issue-code")).ToHaveTextAsync("Cấp lại mã");
        });

        var saved = await E2EDealerData.DealerByTaxCodeAsync(fx.Services, dealer.TaxCode);
        Assert.NotNull(saved!.AccessCodeHash);
        Assert.Equal(64, saved.AccessCodeHash!.Length);
        Assert.NotNull(saved.AccessCodeIssuedAt);
    }

    [E2EFact]
    public async Task Reissued_code_ends_open_dealer_session_and_old_code_is_rejected()
    {
        var product = await E2EDealerData.ProductAsync(fx.Services, "E2E Reissue");
        var dealer = await E2EDealerData.ApprovedCompanyAsync(fx.Services);
        var oldCode = await E2EDealerData.KnownCodeAsync(fx.Services, dealer.Id);

        await using var guest = await fx.NewGuestContextAsync();
        await using var manager = await fx.NewRoleContextAsync(AppRoles.SalesManager);
        var guestPage = await guest.NewPageAsync();
        var cart = new CartPage(guestPage);
        await new ProductDetailPage(guestPage).AddAsync(E2ETestData.CategorySlug, product.Slug, product.Name, 1);
        await cart.GotoAsync();
        await cart.DealerCode.EnterAsync(oldCode);
        await Expect(cart.DealerCode.Active).ToContainTextAsync(dealer.BusinessName);

        await fx.RunAsync(manager, "Sales_manager_reissues_dealer_code", async page =>
        {
            page.Dialog += (_, dialog) => _ = dialog.AcceptAsync();
            var dealers = new AdminDealersPage(page);
            await dealers.SearchAsync(dealer.TaxCode);
            await dealers.OpenDetailAsync(dealer.TaxCode);
            await dealers.Action("issue-code").ClickAsync();
            await dealers.Toasts.ExpectSuccessAsync("Đã cấp mã đại lý mới. Mã cũ không còn hiệu lực.");
        });

        await fx.RunAsync(guest, nameof(Reissued_code_ends_open_dealer_session_and_old_code_is_rejected), async page =>
        {
            var guestCart = new CartPage(page);
            await guestCart.GotoAsync();
            await Expect(guestCart.DealerNotice).ToHaveTextAsync("Mã đại lý đã hết hiệu lực. Vui lòng nhập lại mã.");
            await Expect(guestCart.DealerCode.Active).ToHaveCountAsync(0);

            await guestCart.DealerCode.EnterAsync(oldCode);
            await Expect(guestCart.DealerCode.Error).ToHaveTextAsync(Invalid);
        });
    }
}

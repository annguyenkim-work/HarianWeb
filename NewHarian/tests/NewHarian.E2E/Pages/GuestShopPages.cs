using System.Text.RegularExpressions;

namespace NewHarian.E2E.Pages;

public sealed record GuestCustomer(
    string Name,
    string Email,
    string Phone,
    string CitizenId,
    string ProvinceCode,
    string CommuneCode,
    string AddressLine)
{
    /// <summary>Unique email per test so order lookup never matches another run's data.</summary>
    public static GuestCustomer Unique() => new(
        Name: "Khách E2E",
        Email: $"guest-{Guid.NewGuid():N}@e2e.local",
        Phone: "0901234567",
        CitizenId: "001234567890",
        ProvinceCode: "01",
        CommuneCode: "00004",
        AddressLine: "12 Đường Thử Nghiệm");
}

public enum GuestPayment { Cod, BankTransfer }

public sealed class ProductCatalogPage(IPage page)
{
    public async Task GotoAsync() => await page.GotoAsync("/products");

    public async Task OpenProductAsync(string productName, string categorySlug, string productSlug)
    {
        await Card(productName).ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex(
            $"/products/{Regex.Escape(categorySlug)}/{Regex.Escape(productSlug)}$", RegexOptions.IgnoreCase));
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = productName, Level = 1 })).ToBeVisibleAsync();
    }

    private ILocator Card(string heading)
        => page.Locator("a.card-link", new() { Has = page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }) });
}

public sealed class ProductDetailPage(IPage page)
{
    public async Task SelectVariantAsync(string sku)
    {
        await page.Locator("label.variant-option", new() { Has = page.Locator($"input[name=variantId][data-sku='{sku}']") }).ClickAsync();
        // The variant API call updates SKU text and the hidden cart variant id in the same task.
        await Expect(page.Locator("#variant-sku")).ToHaveTextAsync($"SKU: {sku}");
    }

    public async Task AddToCartAndOpenCartAsync(int quantity)
    {
        await page.Locator("#add-cart-form input[name=quantity]").FillAsync(quantity.ToString());
        await page.Locator("#add-cart-form button[type=submit]").ClickAsync();
        await Expect(page.Locator("#cart-modal")).ToBeVisibleAsync();
        await page.Locator("#cart-modal-view").ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex("/Cart$", RegexOptions.IgnoreCase));
    }
}

public sealed class CartPage(IPage page)
{
    public ILocator Line(string sku) => page.Locator("tr.js-cart-row").Filter(new() { HasText = sku });

    public async Task ProceedToCheckoutAsync()
    {
        await page.Locator(".cart-summary a.btn-primary").ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex("/checkout$", RegexOptions.IgnoreCase));
    }
}

public sealed class CheckoutPage(IPage page)
{
    public async Task FillAndContinueAsync(GuestCustomer customer, GuestPayment payment)
    {
        await page.Locator("#CustomerName").FillAsync(customer.Name);
        await page.Locator("#CustomerEmail").FillAsync(customer.Email);
        await page.Locator("#CustomerPhone").FillAsync(customer.Phone);
        await page.Locator("#CitizenId").FillAsync(customer.CitizenId);
        // Options are loaded by address-fields.js; SelectOption waits until the option exists / select is enabled.
        await page.Locator("#checkout-form select[data-address-province]").SelectOptionAsync(customer.ProvinceCode);
        await page.Locator("#checkout-form select[data-address-commune]").SelectOptionAsync(customer.CommuneCode);
        await page.Locator("#checkout-form [data-address-line]").FillAsync(customer.AddressLine);
        var method = payment == GuestPayment.Cod ? "COD" : "BankTransfer";
        await page.Locator($"#checkout-form input[name=PaymentMethod][value={method}]").CheckAsync();

        await page.Locator("#checkout-form button[type=submit]").ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex("/checkout/confirm$", RegexOptions.IgnoreCase));
    }

    public async Task PlaceOrderAsync()
    {
        await page.Locator("form[action$='/checkout/submit' i] button[type=submit]").ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex("/checkout/success/HAR-ORDER-", RegexOptions.IgnoreCase));
    }
}

public sealed class CheckoutSuccessPage(IPage page)
{
    private static readonly Regex OrderNumberPattern = new(@"^HAR-ORDER-\d{4,}$");

    public ILocator BankBox => page.Locator(".bank-box");
    public ILocator BankQrImage => page.Locator(".bank-box img.bank-qr-img");

    public async Task<string> OrderNumberAsync()
    {
        var orderNo = page.Locator("#order-no");
        await Expect(orderNo).ToHaveTextAsync(OrderNumberPattern);
        return (await orderNo.TextContentAsync())!.Trim();
    }
}

public sealed class OrderTrackPage(IPage page)
{
    public ILocator Result => page.Locator("section.confirm-block");

    public async Task LookupAsync(string orderNumber, string email)
    {
        await page.GotoAsync("/orders/track");
        await page.Locator("#OrderNumber").FillAsync(orderNumber);
        await page.Locator("#CustomerEmail").FillAsync(email);
        await page.Locator("form.form-stack button[type=submit]").ClickAsync();
        await Expect(Result.Locator("h2").First).ToHaveTextAsync(orderNumber);
    }

    public ILocator CancelButton => Result.Locator("form[action$='/orders/track/cancel' i] button[type=submit]");

    /// <summary>Guest self-cancel (unpaid bank transfer only); accepts the confirm() prompt.</summary>
    public async Task CancelAsync()
    {
        EventHandler<IDialog> accept = (_, d) => _ = d.AcceptAsync();
        page.Dialog += accept;
        try
        {
            await CancelButton.ClickAsync();
            await Expect(page.Locator("p.text-success")).ToHaveTextAsync("Đã hủy đơn hàng.");
        }
        finally
        {
            page.Dialog -= accept;
        }
    }
}

public sealed class ServiceBookingPage(IPage page)
{
    private static readonly Regex BookingNumberPattern = new(@"^HAR-SERVICE-\d{4,}$");

    private ILocator Form => page.Locator("form[action$='/book' i]");

    public async Task OpenFromDetailAsync(string categorySlug, string serviceSlug, string serviceName)
    {
        await page.GotoAsync($"/services/{categorySlug}/{serviceSlug}");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = serviceName, Level = 1 })).ToBeVisibleAsync();
        await page.Locator("#book-link").ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex(
            $"/services/{Regex.Escape(categorySlug)}/{Regex.Escape(serviceSlug)}/book", RegexOptions.IgnoreCase));
    }

    /// <summary>Submits the booking form (no CCCD: staff fills it at completion); returns the booking number.</summary>
    public async Task<string> SubmitAsync(GuestCustomer customer, string variantLabel, DateOnly preferredDate)
    {
        await Form.Locator("#ServiceVariantId").SelectOptionAsync(new SelectOptionValue { Label = variantLabel });
        await Form.Locator("#CustomerName").FillAsync(customer.Name);
        await Form.Locator("#CustomerEmail").FillAsync(customer.Email);
        await Form.Locator("#CustomerPhone").FillAsync(customer.Phone);
        await Form.Locator("#PreferredDate").FillAsync(preferredDate.ToString("yyyy-MM-dd"));
        await Form.Locator("#PreferredTime").SelectOptionAsync("Chiều");
        await Form.Locator("select[data-address-province]").SelectOptionAsync(customer.ProvinceCode);
        await Form.Locator("select[data-address-commune]").SelectOptionAsync(customer.CommuneCode);
        await Form.Locator("[data-address-line]").FillAsync(customer.AddressLine);

        await Form.Locator("button[type=submit]").ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex("/book/thanks$", RegexOptions.IgnoreCase));

        var bookingNo = page.Locator("#booking-no");
        await Expect(bookingNo).ToHaveTextAsync(BookingNumberPattern);
        return (await bookingNo.TextContentAsync())!.Trim();
    }
}

/// <summary>Full guest purchase of the E2E product; returns the placed order number.</summary>
public static class GuestShop
{
    public static async Task<string> PlaceOrderAsync(IPage page, GuestCustomer customer, GuestPayment payment, string sku, int quantity)
    {
        var catalog = new ProductCatalogPage(page);
        await catalog.GotoAsync();
        await catalog.OpenProductAsync(
            Fixtures.E2ETestData.ProductName, Fixtures.E2ETestData.CategorySlug, Fixtures.E2ETestData.ProductSlug);

        var detail = new ProductDetailPage(page);
        await detail.SelectVariantAsync(sku);
        await detail.AddToCartAndOpenCartAsync(quantity);

        var cart = new CartPage(page);
        await Expect(cart.Line(sku)).ToBeVisibleAsync();
        await cart.ProceedToCheckoutAsync();

        var checkout = new CheckoutPage(page);
        await checkout.FillAndContinueAsync(customer, payment);
        await checkout.PlaceOrderAsync();

        return await new CheckoutSuccessPage(page).OrderNumberAsync();
    }
}

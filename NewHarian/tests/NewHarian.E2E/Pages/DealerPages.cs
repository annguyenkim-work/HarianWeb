using System.Text.RegularExpressions;

namespace NewHarian.E2E.Pages;

/// <summary>Bảng chiết khấu đại lý (Admin/DealerDiscounts) with the group modal.</summary>
public sealed class AdminDealerDiscountsPage(IPage page)
{
    public ILocator AddButton => page.Locator("#btn-add-group");
    public ILocator Form => page.Locator("#discount-group-form");
    public ILocator TierRows => Form.Locator("[data-tiers] tbody tr[data-tier-key]");
    public AdminToasts Toasts { get; } = new(page);

    public ILocator Group(string name) => page.Locator("section.discount-group").Filter(new() { HasText = name });

    public async Task GotoAsync()
    {
        await page.GotoAsync("/Admin/DealerDiscounts");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Chiết khấu đại lý", Level = 1 })).ToBeVisibleAsync();
    }

    /// <summary>
    /// Creates a group through the modal: <paramref name="tierMaxes"/> are the "Đến" of every tier but the last
    /// (the last is always open-ended); <paramref name="percents"/> holds one % per tier for each product.
    /// </summary>
    public async Task CreateGroupAsync(string name, int[] tierMaxes, params (string ProductName, string[] Percents)[] percents)
    {
        await AddButton.ClickAsync();
        await Expect(Form).ToBeVisibleAsync();
        await Form.Locator("input[name=Name]").FillAsync(name);

        for (var i = 0; i < tierMaxes.Length; i++)
        {
            await Form.Locator("[data-add-tier]").ClickAsync();
            await Expect(TierRows).ToHaveCountAsync(i + 2);
            var max = TierRows.Nth(i).Locator("[data-tier-max]");
            await max.FillAsync(tierMaxes[i].ToString());
            await max.DispatchEventAsync("change");
            await Expect(TierRows.Nth(i + 1).Locator("[data-tier-min]")).ToHaveValueAsync((tierMaxes[i] + 1).ToString());
        }

        foreach (var (productName, values) in percents)
        {
            await Form.Locator($"[data-products] input[type=checkbox][data-product-name=\"{productName}\"]").CheckAsync();
            var row = Form.Locator("[data-matrix] tbody tr").Filter(new() { HasText = productName });
            await Expect(row.Locator("input")).ToHaveCountAsync(values.Length);
            for (var t = 0; t < values.Length; t++)
                await row.Locator("input").Nth(t).FillAsync(values[t]);
        }

        await Form.GetByRole(AriaRole.Button, new() { Name = "Thêm", Exact = true }).ClickAsync();
    }
}

/// <summary>Admin/Dealers list + detail / edit / overrides modal.</summary>
public sealed class AdminDealersPage(IPage page)
{
    public ILocator Modal => page.Locator("#dealer-modal-body");
    public AdminToasts Toasts { get; } = new(page);

    public ILocator Row(string taxCode) => page.Locator("tr[data-dealer-row]").Filter(new() { HasText = taxCode });

    public async Task SearchAsync(string q)
    {
        await page.GotoAsync($"/Admin/Dealers?q={Uri.EscapeDataString(q)}");
        await Expect(Row(q)).ToBeVisibleAsync();
    }

    public async Task OpenDetailAsync(string taxCode)
    {
        await Row(taxCode).Locator(".btn-detail").ClickAsync();
        await Expect(Modal.Locator("h2").First).ToBeVisibleAsync();
    }

    public ILocator Action(string action) => Modal.Locator($"[data-action=\"{action}\"]");

    public ILocator OverrideInput(string productName, string range)
        => Modal.GetByLabel($"{productName} {range}", new() { Exact = true });
}

/// <summary>"Thêm đơn" modal on Admin/Orders.</summary>
public sealed class AdminManualOrderForm(IPage page)
{
    private static readonly Regex OrderNumberInUrl = new(@"[?&]q=(HAR-ORDER-[0-9]+)", RegexOptions.IgnoreCase);

    public ILocator Form => page.Locator("#order-create-modal form.manual-order-form");
    public ILocator Lines => Form.Locator("#manual-order-lines .manual-order-line");
    public ILocator Invoice => Form.Locator("#dealer-invoice");
    public ILocator Summary => Form.Locator("#dealer-discount-summary");
    public ILocator CitizenRequiredMark => Form.Locator("#citizen-required-mark");
    public ILocator ManualPercentInputs => Form.Locator(".js-line-manual");
    public AdminToasts Toasts { get; } = new(page);

    public ILocator LineDiscount(int index) => Lines.Nth(index).Locator(".js-line-discount");

    public async Task OpenAsync()
    {
        await page.GotoAsync("/Admin/Orders");
        await page.Locator("#btn-add-order").ClickAsync();
        await Expect(Form).ToBeVisibleAsync();
    }

    /// <summary>Picks a dealer; contact and shipping address are prefilled from the dealer profile.</summary>
    public async Task SelectDealerAsync(string optionLabel, string communeCode)
    {
        await Form.Locator("select[name=Status]").SelectOptionAsync("Confirmed");
        await Form.Locator("input[name=customerKind][value=dealer]").CheckAsync();
        await Form.Locator("#dealer-select").SelectOptionAsync(new SelectOptionValue { Label = optionLabel });
        await Expect(Invoice).ToBeVisibleAsync();
        await Expect(Form.Locator("select[data-address-commune]")).ToHaveValueAsync(communeCode);
    }

    public async Task SetLineAsync(int index, string sku, int quantity)
    {
        if (index >= await Lines.CountAsync())
        {
            await Form.Locator("#btn-add-line").ClickAsync();
            await Expect(Lines).ToHaveCountAsync(index + 1);
        }
        var line = Lines.Nth(index);
        await line.Locator(".js-sku-input").FillAsync(sku);
        await line.Locator($".sku-suggest__list [data-sku='{sku}']").ClickAsync();
        await Expect(line.Locator(".sku-suggest__list")).ToBeHiddenAsync();
        await line.Locator("input[name$='.Quantity']").FillAsync(quantity.ToString());
    }

    public ILocator LinePrice(int index) => Lines.Nth(index).Locator(".js-sku-price");

    public async Task SetUnitPriceAsync(int index, string rawDigits)
    {
        var price = LinePrice(index);
        await price.ClickAsync();
        await price.FillAsync(rawDigits);
        await price.BlurAsync();
    }

    /// <summary>Submits; the page reloads filtered by the new order. Returns its number.</summary>
    public async Task<string> SubmitAsync()
    {
        await Form.GetByRole(AriaRole.Button, new() { Name = "Tạo đơn", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(OrderNumberInUrl);
        await Toasts.ExpectSuccessAsync("Đã tạo đơn");
        return OrderNumberInUrl.Match(page.Url).Groups[1].Value;
    }
}

/// <summary>Guest "/dealers/register" -> confirm -> thank-you.</summary>
public sealed class DealerRegisterPage(IPage page)
{
    public ILocator Form => page.Locator("#dealer-register-form");
    public ILocator Confirm => page.Locator("#dealer-confirm");

    public async Task GotoAsync()
    {
        await page.GotoAsync("/dealers/register");
        await Expect(Form).ToBeVisibleAsync();
    }

    public Task ChooseTypeAsync(string type) => Form.Locator($"input[name=BusinessType][value={type}]").CheckAsync();

    public Task FillAsync(string name, string value) => Form.Locator($"[name='{name}']").FillAsync(value);

    public async Task HeadOfficeAsync(string provinceCode, string communeCode, string line)
    {
        await Form.Locator("select[name=ProvinceCode]").SelectOptionAsync(provinceCode);
        await Form.Locator("select[name=CommuneCode]").SelectOptionAsync(communeCode);
        await FillAsync("Address", line);
    }

    public async Task SeparateShippingAsync(string provinceCode, string communeCode, string line)
    {
        await Form.Locator("input[type=checkbox][name=ShipToHeadOffice]").UncheckAsync();
        var block = Form.Locator("[data-dealer-shipping]");
        await Expect(block).ToBeVisibleAsync();
        await block.Locator("select[name=ShippingProvinceCode]").SelectOptionAsync(provinceCode);
        await block.Locator("select[name=ShippingCommuneCode]").SelectOptionAsync(communeCode);
        await block.Locator("[name=ShippingAddress]").FillAsync(line);
    }

    public Task AttachLicenseAsync(string fileName, byte[] content, string mimeType)
        => Form.Locator("input[type=file][name=businessLicenseFile]").SetInputFilesAsync(
            new FilePayload { Name = fileName, MimeType = mimeType, Buffer = content });

    public async Task ContinueAsync()
    {
        await Form.Locator("button[type=submit]").ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex("/dealers/confirm$", RegexOptions.IgnoreCase));
    }

    public async Task SendAsync()
    {
        await page.Locator("form[action='/dealers/submit'] button[type=submit]").ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex("/dealers/thank-you$", RegexOptions.IgnoreCase));
    }
}

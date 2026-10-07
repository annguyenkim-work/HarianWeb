using System.Text.RegularExpressions;

namespace NewHarian.E2E.Pages;

/// <summary>Admin header/sidebar. Sidebar links exist in the DOM even while the panel is collapsed.</summary>
public sealed class AdminShell(IPage page)
{
    public ILocator SidebarLink(string label)
        => page.Locator(".admin-sidebar__nav a").Filter(new() { HasTextRegex = new Regex($"^\\s*{Regex.Escape(label)}\\s*$") });

    public ILocator SidebarLinkTo(string path)
        => page.Locator($".admin-sidebar__nav a[href='{path}' i]");

    public ILocator NotifyBadge => page.Locator("#admin-notify-badge");
    public ILocator NotifyItems => page.Locator("#admin-notify-list .admin-notify__item");

    public async Task OpenNotificationsAsync()
    {
        await page.Locator("#admin-notify-btn").ClickAsync();
        await Expect(page.Locator("#admin-notify-panel")).ToBeVisibleAsync();
    }
}

/// <summary>Success / failure toasts (admin-toast.js). Toasts raised while a dialog is open live inside it.</summary>
public sealed class AdminToasts(IPage page)
{
    public ILocator Success => page.Locator(".admin-toast--success");
    public ILocator Error => page.Locator(".admin-toast--error");

    public async Task ExpectSuccessAsync(string text)
        => await Expect(Success.Filter(new() { HasText = text })).ToHaveCountAsync(1);

    public async Task ExpectErrorAsync(string text)
        => await Expect(Error.Filter(new() { HasText = text }).First).ToBeVisibleAsync();
}

/// <summary>
/// Admin list with a detail modal whose status buttons POST and then <c>location.reload()</c> (Orders, Service bookings).
/// </summary>
public abstract class AdminStatusListPage
{
    private readonly string _listPath;
    private readonly string _detailButtonSelector;
    private readonly string _postPathPrefix;

    protected AdminStatusListPage(IPage page, string listPath, string modalBodySelector, string detailButtonSelector, string postPathPrefix)
    {
        Page = page;
        _listPath = listPath;
        _detailButtonSelector = detailButtonSelector;
        _postPathPrefix = postPathPrefix;
        Modal = page.Locator(modalBodySelector);
        Toasts = new AdminToasts(page);
    }

    protected IPage Page { get; }
    public ILocator Modal { get; }
    public AdminToasts Toasts { get; }
    public ILocator HistoryMessages => Modal.Locator(".status-history__message");

    public ILocator Row(string number) => Page.Locator("tbody tr[data-id]").Filter(new() { HasText = number });

    public ILocator ActionButton(string label)
        => Modal.Locator(".order-actions").GetByRole(AriaRole.Button, new() { Name = label, Exact = true });

    public async Task GotoAsync(string number)
    {
        await Page.GotoAsync($"{_listPath}?q={Uri.EscapeDataString(number)}");
        await Expect(Row(number)).ToBeVisibleAsync();
    }

    public async Task ExpectStatusAsync(string number, string status)
        => await Expect(Row(number).Locator("td.col-status")).ToHaveTextAsync(status);

    public async Task OpenDetailAsync(string number)
    {
        await Row(number).Locator(_detailButtonSelector).ClickAsync();
        await Expect(Modal.Locator("h2").First).ToHaveTextAsync(number);
    }

    /// <summary>
    /// Clicks a status action in the detail modal, then waits for the list to reload with
    /// <paramref name="expectedStatus"/> and reopens the modal.
    /// </summary>
    public async Task RunActionAsync(string number, string buttonLabel, string expectedStatus)
    {
        var response = await Page.RunAndWaitForResponseAsync(
            () => ActionButton(buttonLabel).ClickAsync(),
            r => r.Request.Method == "POST" && r.Url.Contains(_postPathPrefix, StringComparison.OrdinalIgnoreCase));
        Assert.True(response.Ok, $"{buttonLabel}: HTTP {response.Status}");

        try
        {
            // Old DOM keeps the previous status until the reload lands, so this waits for the reload.
            await ExpectStatusAsync(number, expectedStatus);
        }
        catch (PlaywrightException ex)
        {
            if (await Toasts.Error.CountAsync() == 0) throw;
            throw new InvalidOperationException(
                $"{buttonLabel} rejected by the app: {await Toasts.Error.Last.InnerTextAsync()}", ex);
        }
        await Page.WaitForLoadStateAsync(LoadState.Load);
        await OpenDetailAsync(number);
    }

    /// <summary>Clicks an action that must be blocked with an error toast; returns the toast text.</summary>
    public async Task<string> ClickExpectingErrorToastAsync(string buttonLabel)
    {
        var before = await Toasts.Error.CountAsync();
        await ActionButton(buttonLabel).ClickAsync();
        await Expect(Toasts.Error).ToHaveCountAsync(before + 1);
        return await Toasts.Error.Last.InnerTextAsync();
    }
}

public sealed class AdminOrdersPage(IPage page)
    : AdminStatusListPage(page, "/Admin/Orders", "#order-modal-body", ".btn-order-detail", "/Admin/Orders/")
{
    public ILocator StockPick => Modal.Locator(".order-stock-pick");
}

public sealed class AdminServiceBookingsPage(IPage page)
    : AdminStatusListPage(page, "/Admin/ServiceBookings", "#booking-modal-body", ".btn-detail", "/Admin/ServiceBookings/")
{
    public ILocator CitizenIdInput => Modal.Locator("input[name=citizenId]");
    public ILocator AmountInput => Modal.Locator("input[name=amount]");
}

public sealed class AdminInventoryHistoryPage(IPage page)
{
    public ILocator Rows => page.Locator("table.data-table tbody tr");

    public async Task GotoAsync()
    {
        await page.GotoAsync("/Admin/Inventory/History");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Kho: Lịch sử xuất nhập", Level = 1 })).ToBeVisibleAsync();
    }

    public ILocator RowWithNote(string note) => Rows.Filter(new() { HasText = note });
}

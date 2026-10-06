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

/// <summary>
/// Admin list with a detail modal whose status buttons POST and then <c>location.reload()</c> (Orders, Service bookings).
/// </summary>
public abstract class AdminStatusListPage
{
    private readonly string _listPath;
    private readonly string _detailButtonSelector;
    private readonly string _postPathPrefix;
    private string? _lastDialog;
    private TaskCompletionSource<string>? _dialogWaiter;

    protected AdminStatusListPage(IPage page, string listPath, string modalBodySelector, string detailButtonSelector, string postPathPrefix)
    {
        Page = page;
        _listPath = listPath;
        _detailButtonSelector = detailButtonSelector;
        _postPathPrefix = postPathPrefix;
        Modal = page.Locator(modalBodySelector);
        // Rejected actions surface as alert(json.error); keep the text for the failure message.
        page.Dialog += async (_, d) =>
        {
            _lastDialog = d.Message;
            _dialogWaiter?.TrySetResult(d.Message);
            await d.DismissAsync();
        };
    }

    protected IPage Page { get; }
    public ILocator Modal { get; }
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
        _lastDialog = null;
        var response = await Page.RunAndWaitForResponseAsync(
            () => ActionButton(buttonLabel).ClickAsync(),
            r => r.Request.Method == "POST" && r.Url.Contains(_postPathPrefix, StringComparison.OrdinalIgnoreCase));
        Assert.True(response.Ok, $"{buttonLabel}: HTTP {response.Status}");

        try
        {
            // Old DOM keeps the previous status until location.reload() lands, so this waits for the reload.
            await ExpectStatusAsync(number, expectedStatus);
        }
        catch (PlaywrightException ex) when (_lastDialog is not null)
        {
            throw new InvalidOperationException($"{buttonLabel} rejected by the app: {_lastDialog}", ex);
        }
        await Page.WaitForLoadStateAsync(LoadState.Load);
        await OpenDetailAsync(number);
    }

    /// <summary>Clicks an action that must be blocked by an alert; returns the alert text.</summary>
    public async Task<string> ClickExpectingAlertAsync(string buttonLabel)
    {
        var waiter = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dialogWaiter = waiter;
        try
        {
            await ActionButton(buttonLabel).ClickAsync();
            return await waiter.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            _dialogWaiter = null;
        }
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

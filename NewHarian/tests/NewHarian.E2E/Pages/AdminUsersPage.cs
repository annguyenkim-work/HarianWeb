namespace NewHarian.E2E.Pages;

/// <summary>Admin Users list + &lt;dialog&gt; modal form (single role dropdown).</summary>
public sealed class AdminUsersPage(IPage page)
{
    public ILocator Dialog => page.Locator("#user-modal");
    public ILocator Form => page.Locator("#user-edit-form");
    public ILocator Email => Form.Locator("input[name=Email]");
    public ILocator FullName => Form.Locator("input[name=FullName]");
    public ILocator Password => Form.Locator("input[name=Password]");
    public ILocator Role => Form.Locator("select[name=Role]");
    public ILocator RoleReviewNote => Form.Locator("[data-role-review]");
    public AdminToasts Toasts { get; } = new(page);

    public ILocator Row(string email) => page.Locator("table.data-table tbody tr").Filter(new() { HasText = email });

    public async Task GotoAsync()
    {
        await page.GotoAsync("/Admin/Users");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Users", Level = 1 })).ToBeVisibleAsync();
    }

    public async Task OpenNewAsync()
    {
        await page.Locator("#btn-add-user").ClickAsync();
        await Expect(Form).ToBeVisibleAsync();
    }

    public async Task OpenEditAsync(string email)
    {
        await Row(email).Locator(".btn-edit-user").ClickAsync();
        await Expect(Form).ToBeVisibleAsync();
    }

    /// <summary>
    /// Submits; on success the page reloads and the queued toast shows only on the new page, so callers wait on
    /// <see cref="AdminToasts.ExpectSuccessAsync"/>.
    /// </summary>
    public async Task SaveAsync()
        => await Form.GetByRole(AriaRole.Button, new() { Name = "Lưu" }).ClickAsync();

    /// <summary>Top edge of two fields' inputs; equal values mean the row is aligned.</summary>
    public static async Task<(float A, float B)> TopsAsync(ILocator a, ILocator b)
    {
        var boxA = await a.BoundingBoxAsync() ?? throw new InvalidOperationException("Field A not visible");
        var boxB = await b.BoundingBoxAsync() ?? throw new InvalidOperationException("Field B not visible");
        return (boxA.Y, boxB.Y);
    }
}

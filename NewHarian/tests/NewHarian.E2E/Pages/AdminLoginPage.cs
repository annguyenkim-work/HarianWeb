namespace NewHarian.E2E.Pages;

public sealed class AdminLoginPage(IPage page)
{
    public async Task LoginAsync(string email, string password)
    {
        await page.GotoAsync("/admin/login");
        await page.Locator("#Email").FillAsync(email);
        await page.Locator("#Password").FillAsync(password);
        await page.Locator(".admin-login button[type=submit]").ClickAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Đăng xuất" })).ToBeVisibleAsync();
    }
}

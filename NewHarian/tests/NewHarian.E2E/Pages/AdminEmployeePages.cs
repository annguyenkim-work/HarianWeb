namespace NewHarian.E2E.Pages;

/// <summary>Personal + work fields shared by the Nhân viên modal and the "Hồ sơ của tôi" page.</summary>
public sealed class EmployeeProfileFields(ILocator form)
{
    public ILocator EmployeeCode => form.Locator("[data-employee-code]");
    public ILocator PersonalSection => form.Locator("[data-section=personal]");
    public ILocator HrSection => form.Locator("[data-section=hr]");
    public ILocator FullName => form.Locator("input[name=FullName]");
    public ILocator DateOfBirth => form.Locator("input[name=DateOfBirth]");
    public ILocator Phone => form.Locator("input[name=Phone]");
    public ILocator CitizenId => form.Locator("input[name=CitizenId]");
    public ILocator Address => form.Locator("input[name=Address]");
    public ILocator Role => form.Locator("[data-employee-role]");
    public ILocator HrNotes => form.Locator("textarea[name=HrNotes]");
    public ILocator HireDate => form.Locator("input[name=HireDate]");
    public ILocator Status => form.Locator("select[name=Status]");
    public ILocator SaveButton => form.GetByRole(AriaRole.Button, new() { Name = "Lưu" });
}

public sealed class MyProfilePage(IPage page)
{
    public ILocator Form => page.Locator("#my-profile-form");
    public EmployeeProfileFields Fields => new(Form);
    public AdminToasts Toasts { get; } = new(page);

    /// <summary>Opens the page through the header link, as an employee would.</summary>
    public async Task OpenFromHeaderAsync()
    {
        await page.GotoAsync("/Admin");
        await page.Locator("#admin-my-profile").ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Hồ sơ của tôi", Level = 1 })).ToBeVisibleAsync();
    }
}

public sealed class AdminEmployeesPage(IPage page)
{
    public ILocator Dialog => page.Locator("#employee-modal");
    public ILocator Form => page.Locator("#employee-edit-form");
    public EmployeeProfileFields Fields => new(Form);
    public AdminToasts Toasts { get; } = new(page);

    public ILocator Row(string email) => page.Locator($"tr[data-employee-row=\"{email}\"]");

    public async Task SearchAsync(string query)
    {
        await page.GotoAsync("/Admin/Employees");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Nhân viên", Level = 1 })).ToBeVisibleAsync();
        await page.Locator("input[name=q]").FillAsync(query);
        await page.GetByRole(AriaRole.Button, new() { Name = "Tìm kiếm" }).ClickAsync();
        await Expect(page.Locator("input[name=q]")).ToHaveValueAsync(query);
    }

    public async Task OpenAsync(string email)
    {
        await Row(email).Locator(".btn-open-employee").ClickAsync();
        await Expect(Form).ToBeVisibleAsync();
    }
}

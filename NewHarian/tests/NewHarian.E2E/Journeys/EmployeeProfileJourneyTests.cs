using NewHarian.Application.Abstractions;
using NewHarian.Application.Hr;
using NewHarian.E2E.Fixtures;
using NewHarian.E2E.Pages;

namespace NewHarian.E2E.Journeys;

[Collection(E2ECollection.Name)]
[Trait("Category", "E2E")]
[Trait("Priority", "P1")]
public sealed class EmployeeProfileJourneyTests(E2EFixture fx)
{
    /// <summary>Admin created the account (seed); the employee fills in the profile, then HR completes the work fields.</summary>
    [E2EFact]
    public async Task Employee_fills_own_profile_then_hr_manager_completes_work_info()
    {
        var email = E2ETestData.EmailFor(AppRoles.WarehouseStaff);
        var phone = $"09{Random.Shared.Next(10_000_000, 99_999_999)}";
        var hrNote = $"Ghi chú E2E {Guid.NewGuid():N}";
        var code = "";

        await using (var staff = await fx.NewRoleContextAsync(AppRoles.WarehouseStaff))
        {
            await fx.RunAsync(staff, "Employee_fills_own_profile", async page =>
            {
                var profile = new MyProfilePage(page);
                await profile.OpenFromHeaderAsync();
                await Expect(profile.Fields.Role).ToHaveValueAsync(AppRoles.Label(AppRoles.WarehouseStaff));
                await Expect(profile.Fields.HireDate).ToBeDisabledAsync();
                await Expect(profile.Fields.Status).ToBeDisabledAsync();
                await Expect(profile.Fields.HrNotes).ToHaveCountAsync(0);
                await Expect(profile.Fields.Phone).ToBeEnabledAsync();

                await profile.Fields.Phone.FillAsync(phone);
                await profile.Fields.CitizenId.FillAsync("123");
                await profile.Fields.SaveButton.ClickAsync();
                await profile.Toasts.ExpectErrorAsync(EmployeeProfilePolicy.CitizenIdInvalid);
                await Expect(profile.Fields.Phone).ToHaveValueAsync(phone);

                await profile.Fields.DateOfBirth.FillAsync("1995-03-02");
                await profile.Fields.CitizenId.FillAsync("001099012345");
                await profile.Fields.Address.FillAsync("12 Lê Lợi, Quận 1, TP.HCM");
                await profile.Fields.SaveButton.ClickAsync();

                await profile.Toasts.ExpectSuccessAsync("Đã lưu hồ sơ của bạn.");
                var expectedCode = EmployeeCodeGenerator.BaseFromFullName($"E2E {AppRoles.Label(AppRoles.WarehouseStaff)}");
                await Expect(profile.Fields.EmployeeCode).ToHaveTextAsync(new System.Text.RegularExpressions.Regex($"^{expectedCode}\\d*$"));
                code = await profile.Fields.EmployeeCode.InnerTextAsync();
            });
        }

        await using var hr = await fx.NewRoleContextAsync(AppRoles.HrManager);
        await fx.RunAsync(hr, "Hr_manager_completes_work_info", async page =>
        {
            var employees = new AdminEmployeesPage(page);
            await employees.SearchAsync(email);
            await Expect(employees.Row(email)).ToContainTextAsync(code);
            await Expect(employees.Row(email).Locator("[data-profile-incomplete]")).ToHaveCountAsync(0);

            await employees.OpenAsync(email);
            await Expect(employees.Fields.Phone).ToHaveValueAsync(phone);
            await Expect(employees.Fields.Role).ToHaveValueAsync(AppRoles.Label(AppRoles.WarehouseStaff));
            await employees.Fields.HireDate.FillAsync("2026-01-05");
            await employees.Fields.Status.SelectOptionAsync("Probation");
            await employees.Fields.HrNotes.FillAsync(hrNote);
            await employees.Fields.SaveButton.ClickAsync();

            await employees.Toasts.ExpectSuccessAsync("Đã lưu hồ sơ nhân viên.");
            await Expect(employees.Row(email)).ToContainTextAsync("Thử việc");
            await Expect(employees.Row(email)).ToContainTextAsync(AppRoles.Label(AppRoles.WarehouseStaff));

            await employees.OpenAsync(email);
            await Expect(employees.Fields.HireDate).ToHaveValueAsync("2026-01-05");
            await Expect(employees.Fields.HrNotes).ToHaveValueAsync(hrNote);
        });
    }

    [E2EFact]
    public async Task Hr_staff_views_employee_profile_read_only()
    {
        var email = E2ETestData.EmailFor(AppRoles.SalesManager);
        await using var context = await fx.NewRoleContextAsync(AppRoles.HrStaff);
        await fx.RunAsync(context, nameof(Hr_staff_views_employee_profile_read_only), async page =>
        {
            var employees = new AdminEmployeesPage(page);
            await employees.SearchAsync(email);
            await employees.OpenAsync(email);

            await Expect(employees.Fields.FullName).ToBeDisabledAsync();
            await Expect(employees.Fields.HireDate).ToBeDisabledAsync();
            await Expect(employees.Fields.SaveButton).ToHaveCountAsync(0);
            await Expect(employees.Dialog.GetByRole(AriaRole.Button, new() { Name = "Đóng" })).ToBeVisibleAsync();
        });
    }

    [E2EFact]
    public async Task Hr_manager_creates_employee_then_super_admin_grants_account()
    {
        var tag = "Ua" + new string(Enumerable.Range(0, 4).Select(_ => (char)('a' + Random.Shared.Next(26))).ToArray());
        var fullName = $"Nguyễn Kim {tag}";
        var expectedCode = EmployeeCodeGenerator.BaseFromFullName(fullName);
        var email = $"{tag.ToLowerInvariant()}@e2e.local";

        await using (var hr = await fx.NewRoleContextAsync(AppRoles.HrManager))
        {
            await fx.RunAsync(hr, "Hr_creates_employee_without_account", async page =>
            {
                var employees = new AdminEmployeesPage(page);
                await employees.CreateAsync(fullName);
                await employees.Toasts.ExpectSuccessAsync("Đã thêm nhân viên.");
                await employees.SearchAsync(tag);
                await Expect(employees.Row(expectedCode)).ToContainTextAsync("Chưa có tài khoản");
                await Expect(employees.Row(expectedCode)).ToContainTextAsync(expectedCode);
            });
        }

        await using var admin = await fx.NewRoleContextAsync(AppRoles.SuperAdmin);
        await fx.RunAsync(admin, "Super_admin_grants_account", async page =>
        {
            var employees = new AdminEmployeesPage(page);
            await employees.SearchAsync(tag);
            await employees.OpenAsync(expectedCode);
            await page.Locator("[data-grant-account]").ClickAsync();
            var grant = page.Locator("#grant-account-form");
            await Expect(grant).ToBeVisibleAsync();
            await grant.Locator("input[name=Email]").FillAsync(email);
            await grant.Locator("input[name=Password]").FillAsync(E2ETestData.UserPassword);
            await grant.Locator("select[name=Role]").SelectOptionAsync(AppRoles.SalesStaff);
            await grant.GetByRole(AriaRole.Button, new() { Name = "Cấp tài khoản" }).ClickAsync();
            await employees.Toasts.ExpectSuccessAsync("Đã cấp tài khoản cho nhân viên.");

            await employees.SearchAsync(email);
            await Expect(employees.Row(email)).ToContainTextAsync(expectedCode);
            await Expect(employees.Row(email).Locator("[data-no-account]")).ToHaveCountAsync(0);
        });
    }
}

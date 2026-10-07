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
                await Expect(profile.Fields.EmployeeCode).ToHaveTextAsync(new System.Text.RegularExpressions.Regex(@"^NV\d{4}$"));
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
}

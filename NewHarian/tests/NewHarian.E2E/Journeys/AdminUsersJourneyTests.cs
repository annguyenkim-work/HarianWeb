using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.E2E.Fixtures;
using NewHarian.E2E.Pages;
using NewHarian.Infrastructure.Identity;

namespace NewHarian.E2E.Journeys;

[Collection(E2ECollection.Name)]
[Trait("Category", "E2E")]
[Trait("Priority", "P0")]
public sealed class AdminUsersJourneyTests(E2EFixture fx)
{
    [E2EFact]
    public async Task Super_admin_creates_user_with_one_role_and_sees_success_then_error_toasts()
    {
        var email = $"e2e-user-{Guid.NewGuid():N}@e2e.local";
        await using var context = await fx.NewRoleContextAsync(AppRoles.SuperAdmin);
        await fx.RunAsync(context, nameof(Super_admin_creates_user_with_one_role_and_sees_success_then_error_toasts), async page =>
        {
            var users = new AdminUsersPage(page);
            await users.GotoAsync();
            await users.OpenNewAsync();

            // Layout: fields on the same row share the same top edge (the password hint sits below its input).
            var (emailTop, nameTop) = await AdminUsersPage.TopsAsync(users.Email, users.FullName);
            Assert.True(Math.Abs(emailTop - nameTop) < 1, $"Email/Họ tên misaligned: {emailTop} vs {nameTop}");
            var (passwordTop, roleTop) = await AdminUsersPage.TopsAsync(users.Password, users.Role);
            Assert.True(Math.Abs(passwordTop - roleTop) < 1, $"Mật khẩu/Role misaligned: {passwordTop} vs {roleTop}");

            await users.Email.FillAsync(email);
            await users.FullName.FillAsync("E2E Nhân viên kho");
            await users.Password.FillAsync(E2ETestData.UserPassword);
            await users.Role.SelectOptionAsync(AppRoles.WarehouseStaff);
            await users.SaveAsync();

            await users.Toasts.ExpectSuccessAsync("Đã thêm user.");
            await Expect(users.Row(email)).ToContainTextAsync("Nhân viên Kho");

            // Same email again: the modal stays open and the error toast carries the server message.
            await users.OpenNewAsync();
            await users.Email.FillAsync(email);
            await users.Password.FillAsync(E2ETestData.UserPassword);
            await users.Role.SelectOptionAsync(AppRoles.SalesStaff);
            await users.SaveAsync();
            await Expect(users.Dialog.Locator(".admin-toast--error")).ToContainTextAsync("Email đã được dùng cho user khác.");
            await Expect(users.Form.Locator(".validation-summary-errors")).ToBeVisibleAsync();

            await page.Keyboard.PressAsync("Escape");
            await users.OpenEditAsync(email);
            await Expect(users.Role).ToHaveValueAsync(AppRoles.WarehouseStaff);
            await Expect(users.Email).ToHaveAttributeAsync("readonly", "");
        });
    }

    [E2EFact]
    public async Task Multi_role_user_is_flagged_until_super_admin_picks_one_role()
    {
        var email = await CreateMultiRoleUserAsync();
        await using var context = await fx.NewRoleContextAsync(AppRoles.SuperAdmin);
        await fx.RunAsync(context, nameof(Multi_role_user_is_flagged_until_super_admin_picks_one_role), async page =>
        {
            var users = new AdminUsersPage(page);
            await users.GotoAsync();
            await Expect(users.Row(email).Locator("[data-role-review]")).ToHaveTextAsync("Cần chọn lại role");

            await users.OpenEditAsync(email);
            await Expect(users.RoleReviewNote).ToBeVisibleAsync();
            await Expect(users.Role).ToHaveValueAsync("");

            await users.Role.SelectOptionAsync(AppRoles.SalesManager);
            await users.SaveAsync();

            await users.Toasts.ExpectSuccessAsync("Đã cập nhật user.");
            await Expect(users.Row(email).Locator("[data-role-review]")).ToHaveCountAsync(0);
            await Expect(users.Row(email)).ToContainTextAsync("Trưởng phòng Kinh doanh");
        });
    }

    private async Task<string> CreateMultiRoleUserAsync()
    {
        using var scope = fx.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"e2e-multi-{Guid.NewGuid():N}@e2e.local";
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, IsActive = true, FullName = "E2E Nhiều role" };
        var created = await users.CreateAsync(user, E2ETestData.UserPassword);
        Assert.True(created.Succeeded, string.Join(";", created.Errors.Select(e => e.Description)));
        await users.AddToRolesAsync(user, [AppRoles.SalesStaff, AppRoles.WarehouseStaff]);
        return email;
    }
}

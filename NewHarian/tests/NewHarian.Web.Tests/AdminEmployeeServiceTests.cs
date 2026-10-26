using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Hr;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Identity;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Tests;

/// <summary>Own factory = own in-memory DB for employee create/link/code flows.</summary>
public class AdminEmployeeServiceTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public AdminEmployeeServiceTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Hr_can_create_employee_without_account_and_gets_name_based_code()
    {
        using var scope = await ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<IAdminEmployeeService>();

        var (ok, error, id) = await service.SaveAsync(new EmployeeProfileForm
        {
            FullName = "Nguyễn Kim An",
            Status = EmployeeStatus.Active
        }, EmployeeEditScope.Hr, "hr-1");

        Assert.True(ok, error);
        Assert.NotNull(id);
        var stored = (await service.GetByIdAsync(id!.Value))!;
        Assert.Equal("AnNK", stored.EmployeeCode);
        Assert.False(stored.HasAccount);
        Assert.Null(stored.UserId);
    }

    [Fact]
    public async Task Legacy_first_save_for_user_creates_profile_with_name_code()
    {
        using var scope = await ScopeAsync();
        var sp = scope.ServiceProvider;
        var service = sp.GetRequiredService<IAdminEmployeeService>();
        var userId = await NewUserAsync(sp, AppRoles.WarehouseStaff);

        var form = (await service.GetByUserIdAsync(userId))!;
        Assert.Null(form.Id);
        form.FullName = "Nguyễn Văn Kho";
        form.Phone = "0909123456";
        Assert.True((await service.SaveAsync(form, EmployeeEditScope.Hr, "hr-1")).Ok);

        var again = (await service.GetByUserIdAsync(userId))!;
        Assert.Equal("KhoNV", again.EmployeeCode);
        Assert.True(again.HasAccount);
        Assert.Equal(1, await sp.GetRequiredService<AppDbContext>().EmployeeProfiles.CountAsync(p => p.UserId == userId));
    }

    [Fact]
    public async Task Name_code_collision_gets_numeric_suffix()
    {
        using var scope = await ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<IAdminEmployeeService>();
        var tag = "Zz" + Guid.NewGuid().ToString("N")[..4];
        var fullName = $"Nguyễn Kim {tag}";
        var baseCode = EmployeeCodeGenerator.BaseFromFullName(fullName);

        Assert.True((await service.SaveAsync(new EmployeeProfileForm { FullName = fullName }, EmployeeEditScope.Hr, null)).Ok);
        var (_, _, id2) = await service.SaveAsync(new EmployeeProfileForm { FullName = fullName }, EmployeeEditScope.Hr, null);
        Assert.Equal($"{baseCode}2", (await service.GetByIdAsync(id2!.Value))!.EmployeeCode);
    }

    [Fact]
    public async Task Grant_account_links_user_to_profile()
    {
        using var scope = await ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<IAdminEmployeeService>();
        var email = $"grant-{Guid.NewGuid():N}@test.local";
        var (_, _, id) = await service.SaveAsync(new EmployeeProfileForm
        {
            FullName = "Trần Hải Hà",
            PersonalEmail = email
        }, EmployeeEditScope.Hr, null);

        var (ok, error) = await service.GrantAccountAsync(new GrantEmployeeAccountRequest
        {
            ProfileId = id!.Value,
            Email = "ignored-different@test.local",
            Password = TestUsers.Password,
            Role = AppRoles.SalesStaff,
            IsActive = true
        }, "admin-1");

        Assert.True(ok, error);
        var stored = (await service.GetByIdAsync(id.Value))!;
        Assert.True(stored.HasAccount);
        Assert.Equal(email, stored.Email);
        Assert.Equal("HaTH", stored.EmployeeCode);
        Assert.Contains(AppRoles.SalesStaff, stored.Roles);
    }

    [Fact]
    public async Task Grant_account_rejects_when_personal_email_missing()
    {
        using var scope = await ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<IAdminEmployeeService>();
        var (_, _, id) = await service.SaveAsync(new EmployeeProfileForm { FullName = "Lê Văn Không Mail" }, EmployeeEditScope.Hr, null);

        var (ok, error) = await service.GrantAccountAsync(new GrantEmployeeAccountRequest
        {
            ProfileId = id!.Value,
            Email = "anything@test.local",
            Password = TestUsers.Password,
            Role = AppRoles.SalesStaff,
            IsActive = true
        }, "admin-1");

        Assert.False(ok);
        Assert.Contains("email cá nhân", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Self_save_ignores_posted_hr_fields()
    {
        using var scope = await ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<IAdminEmployeeService>();
        var userId = await NewUserAsync(scope.ServiceProvider, AppRoles.SalesStaff);
        var hr = (await service.GetByUserIdAsync(userId))!;
        hr.FullName = "Lê Thị Self";
        hr.HireDate = new DateOnly(2026, 2, 1);
        hr.Status = EmployeeStatus.Probation;
        hr.HrNotes = "ghi chú HR";
        await service.SaveAsync(hr, EmployeeEditScope.Hr, "hr-1");

        var self = (await service.GetByUserIdAsync(userId))!;
        self.Phone = "0912000111";
        self.HireDate = new DateOnly(2020, 1, 1);
        self.Status = EmployeeStatus.Active;
        self.HrNotes = "tự sửa";
        Assert.True((await service.SaveAsync(self, EmployeeEditScope.Self, userId)).Ok);

        var stored = (await service.GetByUserIdAsync(userId))!;
        Assert.Equal("0912000111", stored.Phone);
        Assert.Equal(new DateOnly(2026, 2, 1), stored.HireDate);
        Assert.Equal(EmployeeStatus.Probation, stored.Status);
        Assert.Equal("ghi chú HR", stored.HrNotes);
    }

    [Fact]
    public async Task Invalid_form_is_rejected_without_creating_a_profile()
    {
        using var scope = await ScopeAsync();
        var sp = scope.ServiceProvider;
        var service = sp.GetRequiredService<IAdminEmployeeService>();
        var userId = await NewUserAsync(sp, AppRoles.HrStaff);
        var form = (await service.GetByUserIdAsync(userId))!;
        form.CitizenId = "123";

        var (ok, error, _) = await service.SaveAsync(form, EmployeeEditScope.Self, userId);

        Assert.False(ok);
        Assert.Equal(EmployeeProfilePolicy.CitizenIdInvalid, error);
        Assert.False(await sp.GetRequiredService<AppDbContext>().EmployeeProfiles.AnyAsync(p => p.UserId == userId));
    }

    [Fact]
    public async Task Unknown_user_is_not_found()
    {
        using var scope = await ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<IAdminEmployeeService>();

        Assert.Null(await service.GetByUserIdAsync("missing"));
        var (ok, error, _) = await service.SaveAsync(
            new EmployeeProfileForm { UserId = "missing", FullName = "X" }, EmployeeEditScope.Hr, null);
        Assert.False(ok);
        Assert.Equal("Không tìm thấy nhân viên.", error);
    }

    [Fact]
    public async Task List_includes_profiles_without_account_and_legacy_users()
    {
        using var scope = await ScopeAsync();
        var sp = scope.ServiceProvider;
        var service = sp.GetRequiredService<IAdminEmployeeService>();
        var noProfile = await NewUserAsync(sp, AppRoles.WarehouseManager);
        var (_, _, profileId) = await service.SaveAsync(
            new EmployeeProfileForm { FullName = "Văn Thị Thu Thủy", Status = EmployeeStatus.OnLeave },
            EmployeeEditScope.Hr, null);

        var all = await service.ListAsync(null, null);
        Assert.Contains(all, r => r.UserId == noProfile && r.ProfileId is null);
        var orphan = Assert.Single(all, r => r.ProfileId == profileId);
        Assert.Equal("ThuyVTT", orphan.EmployeeCode);
        Assert.False(orphan.HasAccount);

        var onLeave = await service.ListAsync(null, EmployeeStatus.OnLeave);
        Assert.Contains(onLeave, r => r.ProfileId == profileId);
        Assert.DoesNotContain(onLeave, r => r.UserId == noProfile);
    }

    [Fact]
    public async Task List_excludes_accounts_without_an_internal_role()
    {
        using var scope = await ScopeAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"norole-{Guid.NewGuid():N}@test.local";
        await users.CreateAsync(new ApplicationUser { UserName = email, Email = email, IsActive = true }, TestUsers.Password);

        var list = await scope.ServiceProvider.GetRequiredService<IAdminEmployeeService>().ListAsync(null, null);

        Assert.DoesNotContain(list, r => r.Email == email);
    }

    private async Task<IServiceScope> ScopeAsync()
    {
        var scope = _factory.Services.CreateScope();
        await TestUsers.EnsureRolesAsync(scope.ServiceProvider);
        return scope;
    }

    private static async Task<string> NewUserAsync(IServiceProvider sp, string role)
    {
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"emp-{Guid.NewGuid():N}@test.local";
        var user = new ApplicationUser { UserName = email, Email = email, IsActive = true, FullName = "Emp" };
        Assert.True((await users.CreateAsync(user, TestUsers.Password)).Succeeded);
        await users.AddToRoleAsync(user, role);
        return user.Id;
    }
}

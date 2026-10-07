using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Hr;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Identity;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Tests;

/// <summary>Own factory = own in-memory DB, so employee codes start at NV0001.</summary>
public class AdminEmployeeServiceTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public AdminEmployeeServiceTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task First_save_creates_one_profile_with_next_code_and_updates_full_name()
    {
        using var scope = await ScopeAsync();
        var sp = scope.ServiceProvider;
        var service = sp.GetRequiredService<IAdminEmployeeService>();
        var userId = await NewUserAsync(sp, AppRoles.WarehouseStaff);

        var form = (await service.GetAsync(userId))!;
        Assert.Null(form.EmployeeCode);
        form.FullName = "Nguyễn Văn Kho";
        form.Phone = "0909123456";
        form.HireDate = new DateOnly(2026, 3, 1);
        Assert.Equal((true, (string?)null), await service.SaveAsync(form, EmployeeEditScope.Hr, "hr-1"));

        var again = (await service.GetAsync(userId))!;
        Assert.Matches("^NV\\d{4}$", again.EmployeeCode);
        Assert.Equal("Nguyễn Văn Kho", again.FullName);
        Assert.Equal(new DateOnly(2026, 3, 1), again.HireDate);

        Assert.True((await service.SaveAsync(again, EmployeeEditScope.Hr, "hr-1")).Ok);
        var db = sp.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.EmployeeProfiles.CountAsync(p => p.UserId == userId));
        Assert.Equal(again.EmployeeCode, (await service.GetAsync(userId))!.EmployeeCode);
    }

    [Fact]
    public async Task Each_new_profile_gets_a_distinct_code()
    {
        using var scope = await ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<IAdminEmployeeService>();
        var a = await NewUserAsync(scope.ServiceProvider, AppRoles.SalesStaff);
        var b = await NewUserAsync(scope.ServiceProvider, AppRoles.SalesStaff);

        await service.SaveAsync((await service.GetAsync(a))!, EmployeeEditScope.Self, a);
        await service.SaveAsync((await service.GetAsync(b))!, EmployeeEditScope.Self, b);

        Assert.NotEqual((await service.GetAsync(a))!.EmployeeCode, (await service.GetAsync(b))!.EmployeeCode);
    }

    [Fact]
    public async Task Self_save_ignores_posted_hr_fields()
    {
        using var scope = await ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<IAdminEmployeeService>();
        var userId = await NewUserAsync(scope.ServiceProvider, AppRoles.SalesStaff);
        var hr = (await service.GetAsync(userId))!;
        hr.HireDate = new DateOnly(2026, 2, 1);
        hr.Status = EmployeeStatus.Probation;
        hr.HrNotes = "ghi chú HR";
        await service.SaveAsync(hr, EmployeeEditScope.Hr, "hr-1");

        var self = (await service.GetAsync(userId))!;
        self.Phone = "0912000111";
        self.HireDate = new DateOnly(2020, 1, 1);
        self.Status = EmployeeStatus.Active;
        self.HrNotes = "tự sửa";
        Assert.True((await service.SaveAsync(self, EmployeeEditScope.Self, userId)).Ok);

        var stored = (await service.GetAsync(userId))!;
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
        var form = (await service.GetAsync(userId))!;
        form.CitizenId = "123";

        var (ok, error) = await service.SaveAsync(form, EmployeeEditScope.Self, userId);

        Assert.False(ok);
        Assert.Equal(EmployeeProfilePolicy.CitizenIdInvalid, error);
        Assert.False(await sp.GetRequiredService<AppDbContext>().EmployeeProfiles.AnyAsync(p => p.UserId == userId));
    }

    [Fact]
    public async Task Unknown_user_is_not_found()
    {
        using var scope = await ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<IAdminEmployeeService>();

        Assert.Null(await service.GetAsync("missing"));
        var (ok, error) = await service.SaveAsync(new EmployeeProfileForm { UserId = "missing", FullName = "X" }, EmployeeEditScope.Hr, null);
        Assert.False(ok);
        Assert.Equal("Không tìm thấy nhân viên.", error);
    }

    [Fact]
    public async Task List_shows_internal_users_without_profile_and_filters_by_query_and_status()
    {
        using var scope = await ScopeAsync();
        var sp = scope.ServiceProvider;
        var service = sp.GetRequiredService<IAdminEmployeeService>();
        var noProfile = await NewUserAsync(sp, AppRoles.WarehouseManager);
        var withProfile = await NewUserAsync(sp, AppRoles.SalesManager);
        var form = (await service.GetAsync(withProfile))!;
        form.Status = EmployeeStatus.OnLeave;
        await service.SaveAsync(form, EmployeeEditScope.Hr, "hr-1");
        var code = (await service.GetAsync(withProfile))!.EmployeeCode!;

        var all = await service.ListAsync(null, null);
        var bare = Assert.Single(all, r => r.UserId == noProfile);
        Assert.Null(bare.EmployeeCode);
        Assert.False(bare.IsPersonalComplete);
        Assert.Equal([AppRoles.WarehouseManager], bare.Roles);

        var byRoleLabel = await service.ListAsync(AppRoles.Label(AppRoles.WarehouseManager).ToUpperInvariant(), null);
        Assert.Equal(noProfile, Assert.Single(byRoleLabel).UserId);

        var byCode = await service.ListAsync(code, null);
        Assert.Equal(withProfile, Assert.Single(byCode).UserId);

        var onLeave = await service.ListAsync(null, EmployeeStatus.OnLeave);
        Assert.Contains(onLeave, r => r.UserId == withProfile);
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

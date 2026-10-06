using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Infrastructure.Identity;

namespace NewHarian.Web.Tests;

/// <summary>Own factory = own in-memory DB, so Super Admin counts are deterministic.</summary>
public class AdminUserServiceTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public AdminUserServiceTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_rejects_legacy_staff_and_unknown_roles()
    {
        using var scope = await ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<IAdminUserService>();

        var (ok, error, _) = await service.SaveAsync(NewUser("legacy-only", AppRoles.LegacyStaff, "Admin"), actorUserId: null);

        Assert.False(ok);
        Assert.Equal("Chọn ít nhất một role.", error);
    }

    [Fact]
    public async Task Create_requires_password()
    {
        using var scope = await ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<IAdminUserService>();
        var request = NewUser("no-password", AppRoles.SalesStaff);
        request.Password = null;

        var (ok, _, _) = await service.SaveAsync(request, actorUserId: null);

        Assert.False(ok);
    }

    [Fact]
    public async Task Super_admin_cannot_deactivate_or_demote_self()
    {
        using var scope = await ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<IAdminUserService>();
        await service.SaveAsync(NewUser("sa-backup", AppRoles.SuperAdmin), actorUserId: null);
        var (_, _, selfId) = await service.SaveAsync(NewUser("sa-self", AppRoles.SuperAdmin), actorUserId: null);

        var deactivate = (await service.GetForEditAsync(selfId!))!;
        deactivate.IsActive = false;
        Assert.False((await service.SaveAsync(deactivate, selfId)).Ok);

        var demote = (await service.GetForEditAsync(selfId!))!;
        demote.Roles = [AppRoles.HrManager];
        Assert.False((await service.SaveAsync(demote, selfId)).Ok);
    }

    [Fact]
    public async Task Role_change_rotates_security_stamp_and_keeps_legacy_staff_only_while_ticked()
    {
        using var scope = await ScopeAsync();
        var sp = scope.ServiceProvider;
        var service = sp.GetRequiredService<IAdminUserService>();
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();

        var legacy = new ApplicationUser { UserName = "legacy@test.local", Email = "legacy@test.local", IsActive = true };
        await users.CreateAsync(legacy, TestUsers.Password);
        await users.AddToRoleAsync(legacy, AppRoles.LegacyStaff);
        var stampBefore = await users.GetSecurityStampAsync(legacy);

        var edit = (await service.GetForEditAsync(legacy.Id))!;
        Assert.Contains(AppRoles.LegacyStaff, edit.Roles);
        edit.Roles = [AppRoles.LegacyStaff, AppRoles.WarehouseStaff];
        Assert.True((await service.SaveAsync(edit, actorUserId: "someone-else")).Ok);

        edit = (await service.GetForEditAsync(legacy.Id))!;
        Assert.Equal(new[] { AppRoles.WarehouseStaff, AppRoles.LegacyStaff }, edit.Roles);

        edit.Roles = [AppRoles.WarehouseStaff];
        Assert.True((await service.SaveAsync(edit, actorUserId: "someone-else")).Ok);

        var reloaded = await users.FindByIdAsync(legacy.Id);
        Assert.Equal(new[] { AppRoles.WarehouseStaff }, await users.GetRolesAsync(reloaded!));
        Assert.NotEqual(stampBefore, await users.GetSecurityStampAsync(reloaded!));
    }

    private async Task<IServiceScope> ScopeAsync()
    {
        var scope = _factory.Services.CreateScope();
        await TestUsers.EnsureRolesAsync(scope.ServiceProvider);
        return scope;
    }

    internal static AdminUserSaveRequest NewUser(string name, params string[] roles) => new()
    {
        Email = $"{name}-{Guid.NewGuid():N}@test.local",
        FullName = name,
        Password = TestUsers.Password,
        IsActive = true,
        Roles = roles.ToList()
    };
}

/// <summary>Isolated DB: must start with zero Super Admins.</summary>
public class AdminUserServiceLastSuperAdminTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public AdminUserServiceLastSuperAdminTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Last_active_super_admin_cannot_be_demoted_until_another_exists()
    {
        using var scope = _factory.Services.CreateScope();
        await TestUsers.EnsureRolesAsync(scope.ServiceProvider);
        var service = scope.ServiceProvider.GetRequiredService<IAdminUserService>();

        var (_, _, firstId) = await service.SaveAsync(AdminUserServiceTests.NewUser("sa-first", AppRoles.SuperAdmin), actorUserId: null);
        var demote = (await service.GetForEditAsync(firstId!))!;
        demote.Roles = [AppRoles.SalesManager];

        var (blocked, error, _) = await service.SaveAsync(demote, actorUserId: "someone-else");
        Assert.False(blocked);
        Assert.Equal("Phải còn ít nhất một Super Admin đang hoạt động.", error);

        await service.SaveAsync(AdminUserServiceTests.NewUser("sa-second", AppRoles.SuperAdmin), actorUserId: null);
        var (allowed, _, _) = await service.SaveAsync(demote, actorUserId: "someone-else");
        Assert.True(allowed);
    }
}

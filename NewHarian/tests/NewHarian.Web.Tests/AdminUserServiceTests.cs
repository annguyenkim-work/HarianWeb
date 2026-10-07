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

    [Theory]
    [InlineData(null, UserRolePolicy.RoleRequired)]
    [InlineData(AppRoles.LegacyStaff, UserRolePolicy.RoleInvalid)]
    [InlineData("Admin", UserRolePolicy.RoleInvalid)]
    public async Task Create_rejects_missing_legacy_and_unknown_role(string? role, string expected)
    {
        using var scope = await ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<IAdminUserService>();

        var (ok, error, _) = await service.SaveAsync(NewUser("bad-role", role), actorUserId: null);

        Assert.False(ok);
        Assert.Equal(expected, error);
    }

    [Fact]
    public async Task Create_assigns_exactly_the_picked_role()
    {
        using var scope = await ScopeAsync();
        var sp = scope.ServiceProvider;
        var service = sp.GetRequiredService<IAdminUserService>();

        var (ok, _, id) = await service.SaveAsync(NewUser("one-role", AppRoles.WarehouseStaff), actorUserId: null);

        Assert.True(ok);
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Equal(new[] { AppRoles.WarehouseStaff }, await users.GetRolesAsync((await users.FindByIdAsync(id!))!));
    }

    [Fact]
    public async Task Create_rejects_duplicate_email_with_vietnamese_message()
    {
        using var scope = await ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<IAdminUserService>();
        var first = NewUser("dup", AppRoles.SalesStaff);
        Assert.True((await service.SaveAsync(first, actorUserId: null)).Ok);

        var again = NewUser("dup", AppRoles.HrStaff);
        again.Email = first.Email;
        var (ok, error, _) = await service.SaveAsync(again, actorUserId: null);

        Assert.False(ok);
        Assert.Equal("Email đã được dùng cho user khác.", error);
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
        demote.Role = AppRoles.HrManager;
        Assert.False((await service.SaveAsync(demote, selfId)).Ok);
    }

    [Fact]
    public async Task Multi_role_user_needs_a_pick_and_saving_one_role_replaces_all()
    {
        using var scope = await ScopeAsync();
        var sp = scope.ServiceProvider;
        var service = sp.GetRequiredService<IAdminUserService>();
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();

        var legacy = new ApplicationUser { UserName = $"multi-{Guid.NewGuid():N}@test.local", Email = $"multi-{Guid.NewGuid():N}@test.local", IsActive = true };
        await users.CreateAsync(legacy, TestUsers.Password);
        await users.AddToRolesAsync(legacy, [AppRoles.LegacyStaff, AppRoles.WarehouseStaff]);
        var stampBefore = await users.GetSecurityStampAsync(legacy);

        var edit = (await service.GetForEditAsync(legacy.Id))!;
        Assert.Null(edit.Role);
        Assert.Equal(new[] { AppRoles.WarehouseStaff, AppRoles.LegacyStaff }, edit.CurrentRoles);
        Assert.Contains(await service.ListAsync(), u => u.Id == legacy.Id && UserRolePolicy.NeedsRoleReview(u.Roles));

        Assert.Equal(UserRolePolicy.RoleRequired, (await service.SaveAsync(edit, actorUserId: "someone-else")).Error);

        edit.Role = AppRoles.WarehouseStaff;
        Assert.True((await service.SaveAsync(edit, actorUserId: "someone-else")).Ok);

        var reloaded = await users.FindByIdAsync(legacy.Id);
        Assert.Equal(new[] { AppRoles.WarehouseStaff }, await users.GetRolesAsync(reloaded!));
        Assert.NotEqual(stampBefore, await users.GetSecurityStampAsync(reloaded!));
        Assert.Equal(AppRoles.WarehouseStaff, (await service.GetForEditAsync(legacy.Id))!.Role);
    }

    [Fact]
    public async Task Legacy_staff_can_be_kept_but_not_reassigned_after_leaving_it()
    {
        using var scope = await ScopeAsync();
        var sp = scope.ServiceProvider;
        var service = sp.GetRequiredService<IAdminUserService>();
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();

        var legacy = new ApplicationUser { UserName = $"legacy-{Guid.NewGuid():N}@test.local", Email = $"legacy-{Guid.NewGuid():N}@test.local", IsActive = true };
        await users.CreateAsync(legacy, TestUsers.Password);
        await users.AddToRoleAsync(legacy, AppRoles.LegacyStaff);

        var edit = (await service.GetForEditAsync(legacy.Id))!;
        Assert.Equal(AppRoles.LegacyStaff, edit.Role);
        Assert.True((await service.SaveAsync(edit, actorUserId: "someone-else")).Ok);

        edit.Role = AppRoles.HrStaff;
        Assert.True((await service.SaveAsync(edit, actorUserId: "someone-else")).Ok);

        edit = (await service.GetForEditAsync(legacy.Id))!;
        edit.Role = AppRoles.LegacyStaff;
        Assert.Equal(UserRolePolicy.RoleInvalid, (await service.SaveAsync(edit, actorUserId: "someone-else")).Error);
    }

    private async Task<IServiceScope> ScopeAsync()
    {
        var scope = _factory.Services.CreateScope();
        await TestUsers.EnsureRolesAsync(scope.ServiceProvider);
        return scope;
    }

    internal static AdminUserSaveRequest NewUser(string name, string? role) => new()
    {
        Email = $"{name}-{Guid.NewGuid():N}@test.local",
        FullName = name,
        Password = TestUsers.Password,
        IsActive = true,
        Role = role
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
        demote.Role = AppRoles.SalesManager;

        var (blocked, error, _) = await service.SaveAsync(demote, actorUserId: "someone-else");
        Assert.False(blocked);
        Assert.Equal("Phải còn ít nhất một Super Admin đang hoạt động.", error);

        await service.SaveAsync(AdminUserServiceTests.NewUser("sa-second", AppRoles.SuperAdmin), actorUserId: null);
        var (allowed, _, _) = await service.SaveAsync(demote, actorUserId: "someone-else");
        Assert.True(allowed);
    }
}

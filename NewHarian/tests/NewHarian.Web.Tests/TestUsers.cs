using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Infrastructure.Identity;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Tests;

internal static class TestUsers
{
    public const string Password = "Perm@12345";

    public static async Task EnsureRolesAsync(IServiceProvider sp)
    {
        await sp.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.Assignable.Append(AppRoles.LegacyStaff))
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));
        }
    }

    /// <summary>Creates (once) a user holding exactly <paramref name="role"/>; returns its email.</summary>
    public static async Task<string> EnsureUserAsync(NewHarianWebApplicationFactory factory, string role)
    {
        var email = $"{role.ToLowerInvariant()}-test@test.local";
        using var scope = factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        await EnsureRolesAsync(sp);
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(email) is null)
        {
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, IsActive = true };
            var created = await users.CreateAsync(user, Password);
            Assert.True(created.Succeeded, string.Join(";", created.Errors.Select(e => e.Description)));
            await users.AddToRoleAsync(user, role);
        }
        return email;
    }

    public static async Task<HttpClient> LoggedInClientAsync(NewHarianWebApplicationFactory factory, string role, bool allowRedirect = true)
    {
        var email = await EnsureUserAsync(factory, role);
        var client = factory.CreateClient(new() { AllowAutoRedirect = allowRedirect });
        await AdminLoginHelper.LoginAsync(client, email, Password);
        return client;
    }
}

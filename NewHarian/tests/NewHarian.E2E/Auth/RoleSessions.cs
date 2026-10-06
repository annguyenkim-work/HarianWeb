using System.Collections.Concurrent;
using NewHarian.Application.Abstractions;
using NewHarian.E2E.Fixtures;
using NewHarian.E2E.Pages;

namespace NewHarian.E2E.Auth;

/// <summary>
/// Logs each role in through the real login form once per run and caches the Playwright StorageState file.
/// Admin login is rate limited per IP, so journeys reuse the cached cookies instead of logging in again.
/// </summary>
public sealed class RoleSessions(IBrowser browser, string baseUrl, string stateDir)
{
    private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _states = new(StringComparer.Ordinal);

    public Task<string> StorageStatePathAsync(string role)
        => _states.GetOrAdd(role, r => new Lazy<Task<string>>(() => LoginAndSaveAsync(r))).Value;

    private async Task<string> LoginAndSaveAsync(string role)
    {
        var (email, password) = Credentials(role);
        Directory.CreateDirectory(stateDir);
        var path = Path.Combine(stateDir, $"{role}.json");

        await using var context = await browser.NewContextAsync(new() { BaseURL = baseUrl });
        var page = await context.NewPageAsync();
        await new AdminLoginPage(page).LoginAsync(email, password);
        await context.StorageStateAsync(new() { Path = path });
        return path;
    }

    public static (string Email, string Password) Credentials(string role)
        => role == AppRoles.SuperAdmin
            ? (E2ESettings.AdminEmail, E2ESettings.AdminPassword)
            : (E2ETestData.EmailFor(role), E2ETestData.UserPassword);
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.E2E.Auth;
using NewHarian.Infrastructure.DependencyInjection;

namespace NewHarian.E2E.Fixtures;

[CollectionDefinition(Name)]
public sealed class E2ECollection : ICollectionFixture<E2EFixture>
{
    public const string Name = "E2E";
}

/// <summary>
/// One app + one browser per run. In-process mode drops/recreates the E2E Postgres DB and hosts the app on Kestrel;
/// with E2E_BASE_URL it targets a running app (started with ASPNETCORE_ENVIRONMENT=E2E against the same DB).
/// </summary>
public sealed class E2EFixture : IAsyncLifetime
{
    private E2EAppFactory? _app;
    private ServiceProvider? _externalServices;
    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = null!;
    public string BaseUrl { get; private set; } = "";
    /// <summary>App services (in-process) or Infrastructure against the same DB (external mode); for seeding and DB assertions.</summary>
    public IServiceProvider Services { get; private set; } = null!;
    public RoleSessions Sessions { get; private set; } = null!;
    public string ArtifactsDir { get; } = E2ESettings.ArtifactsDir;

    public async Task InitializeAsync()
    {
        var connectionString = E2ESettings.ConnectionString;
        if (E2EDatabase.Probe(connectionString) is { } dbError)
        {
            if (!E2ESettings.Required) return; // every test is skipped by E2EFactAttribute
            throw new InvalidOperationException($"E2E_REQUIRED is set but PostgreSQL is not reachable: {dbError}");
        }

        IServiceProvider services;
        if (E2ESettings.BaseUrl is { } externalUrl)
        {
            BaseUrl = externalUrl;
            _externalServices = BuildExternalServices(connectionString);
            services = _externalServices;
        }
        else
        {
            await E2EDatabase.DropAsync(connectionString);
            _app = new E2EAppFactory(connectionString);
            _app.Start();
            BaseUrl = _app.BaseUrl;
            services = _app.Services;
        }

        Services = services;
        await E2ETestData.SeedAsync(services);

        _playwright = await Playwright.CreateAsync();
        SetDefaultExpectTimeout(15_000);
        Browser = await _playwright.Chromium.LaunchAsync(new()
        {
            Headless = !E2ESettings.Headed,
            SlowMo = E2ESettings.SlowMoMs
        });
        Sessions = new RoleSessions(Browser, BaseUrl, Path.Combine(ArtifactsDir, "auth"));
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        _playwright?.Dispose();
        if (_app is not null) await _app.DisposeAsync();
        if (_externalServices is not null) await _externalServices.DisposeAsync();
    }

    public Task<IBrowserContext> NewGuestContextAsync() => NewContextAsync(storageStatePath: null);

    public async Task<IBrowserContext> NewRoleContextAsync(string role)
        => await NewContextAsync(await Sessions.StorageStatePathAsync(role));

    private async Task<IBrowserContext> NewContextAsync(string? storageStatePath)
    {
        var context = await Browser.NewContextAsync(new()
        {
            BaseURL = BaseUrl,
            Locale = "vi-VN",
            StorageStatePath = storageStatePath,
            ViewportSize = new() { Width = 1366, Height = 900 }
        });
        context.SetDefaultTimeout(15_000);
        context.SetDefaultNavigationTimeout(30_000);
        // Cookie banner is fixed at the bottom and can cover buttons; accept it up front.
        await context.AddCookiesAsync([new Cookie { Name = "cookie_consent", Value = "1", Url = BaseUrl }]);
        return context;
    }

    /// <summary>
    /// Runs one journey with Playwright tracing. On failure the trace (open with `playwright.ps1 show-trace`) and a
    /// full-page screenshot are written to <see cref="ArtifactsDir"/>; on success the trace is discarded.
    /// </summary>
    public async Task RunAsync(IBrowserContext context, string testName, Func<IPage, Task> journey)
    {
        await context.Tracing.StartAsync(new() { Title = testName, Screenshots = true, Snapshots = true, Sources = false });
        var page = await context.NewPageAsync();
        try
        {
            await journey(page);
            await context.Tracing.StopAsync();
        }
        catch
        {
            var safe = string.Concat(testName.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
            var dir = Path.Combine(ArtifactsDir, "failures");
            Directory.CreateDirectory(dir);
            try { await page.ScreenshotAsync(new() { Path = Path.Combine(dir, $"{safe}.png"), FullPage = true }); } catch { /* page may be gone */ }
            await context.Tracing.StopAsync(new() { Path = Path.Combine(dir, $"{safe}.trace.zip") });
            throw;
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    private static ServiceProvider BuildExternalServices(string connectionString)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connectionString })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);
        return services.BuildServiceProvider();
    }
}

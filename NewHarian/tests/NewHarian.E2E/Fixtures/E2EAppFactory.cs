using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace NewHarian.E2E.Fixtures;

/// <summary>
/// Real app on Kestrel (loopback, dynamic port) in environment <c>E2E</c>, so DbSeeder migrates + seeds Postgres
/// and <c>appsettings.E2E.json</c> (relaxed rate limits, SMTP off) applies.
/// </summary>
public sealed class E2EAppFactory : WebApplicationFactory<Program>
{
    public const string EnvironmentName = "E2E";

    private readonly string _connectionString;

    public E2EAppFactory(string connectionString)
    {
        _connectionString = connectionString;
        UseKestrel(o => o.Listen(IPAddress.Loopback, 0));
    }

    /// <summary>App_Data (email outbox, uploaded CVs) goes here instead of the Web project folder.</summary>
    public string AppDataContentRoot { get; } =
        Path.Combine(Path.GetTempPath(), "nh-e2e-" + Guid.NewGuid().ToString("N"));

    public string BaseUrl { get; private set; } = "";

    public void Start()
    {
        StartServer();
        var addresses = Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;
        BaseUrl = addresses?.FirstOrDefault()?.TrimEnd('/')
                  ?? throw new InvalidOperationException("Kestrel did not report a listening address.");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("Database:UseInMemory", "false");
        builder.UseSetting("Email:Smtp:Enabled", "false");

        builder.ConfigureTestServices(services =>
        {
            var inner = services.Last(d => d.ServiceType == typeof(IWebHostEnvironment)).ImplementationInstance
                as IWebHostEnvironment
                ?? throw new InvalidOperationException("IWebHostEnvironment is not registered as an instance.");
            Directory.CreateDirectory(AppDataContentRoot);
            services.AddSingleton<IWebHostEnvironment>(new TempContentRootEnvironment(inner, AppDataContentRoot));
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        try { Directory.Delete(AppDataContentRoot, recursive: true); } catch { /* best effort */ }
    }

    /// <summary>Only ContentRootPath moves; views, static files and config keep the real Web project roots.</summary>
    private sealed class TempContentRootEnvironment(IWebHostEnvironment inner, string contentRoot) : IWebHostEnvironment
    {
        public string ContentRootPath { get; set; } = contentRoot;
        public string EnvironmentName { get => inner.EnvironmentName; set => inner.EnvironmentName = value; }
        public string ApplicationName { get => inner.ApplicationName; set => inner.ApplicationName = value; }
        public IFileProvider ContentRootFileProvider { get => inner.ContentRootFileProvider; set => inner.ContentRootFileProvider = value; }
        public string WebRootPath { get => inner.WebRootPath; set => inner.WebRootPath = value; }
        public IFileProvider WebRootFileProvider { get => inner.WebRootFileProvider; set => inner.WebRootFileProvider = value; }
    }
}

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace NewHarian.Web.Tests;

public sealed class NewHarianWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "NewHarianSecurityTests_" + Guid.NewGuid().ToString("N");

    /// <summary>App_Data (private CVs, email outbox/queue) goes here instead of the Web project folder.</summary>
    public string AppDataContentRoot { get; } =
        Path.Combine(Path.GetTempPath(), "nh-webtests-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting is applied early enough for Program.cs → AddInfrastructure.
        builder.UseSetting("Database:UseInMemory", "true");
        builder.UseSetting("Database:InMemoryName", _dbName);
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=unused;Database=unused");
        builder.UseSetting("Email:Smtp:Enabled", "false");
        builder.UseSetting("Email:Smtp:Password", "");
        builder.UseSetting("Email:Smtp:User", "");
        builder.UseSetting("Email:Smtp:From", "test@example.com");

        builder.ConfigureTestServices(services =>
        {
            var inner = services.Last(d => d.ServiceType == typeof(IWebHostEnvironment)).ImplementationInstance
                as IWebHostEnvironment
                ?? throw new InvalidOperationException("IWebHostEnvironment is not registered as an instance.");
            Directory.CreateDirectory(AppDataContentRoot);
            services.AddSingleton<IWebHostEnvironment>(new TempContentRootEnvironment(inner, AppDataContentRoot));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        try { Directory.Delete(AppDataContentRoot, recursive: true); } catch { /* best effort */ }
    }

    /// <summary>
    /// Only <see cref="ContentRootPath"/> moves (services build App_Data paths from it);
    /// views, static files and config keep using the real Web project roots.
    /// </summary>
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

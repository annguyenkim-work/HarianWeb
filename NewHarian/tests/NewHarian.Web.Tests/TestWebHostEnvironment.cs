using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace NewHarian.Web.Tests;

/// <summary>Minimal <see cref="IWebHostEnvironment"/> rooted in a temp folder for storage tests without the web host.</summary>
internal sealed class TestWebHostEnvironment : IWebHostEnvironment
{
    public TestWebHostEnvironment(string contentRoot, string webRoot)
    {
        ContentRootPath = contentRoot;
        WebRootPath = webRoot;
        ContentRootFileProvider = new PhysicalFileProvider(
            Directory.Exists(contentRoot) ? contentRoot : Path.GetTempPath());
    }

    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "Tests";
    public string ContentRootPath { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; }
    public string WebRootPath { get; set; }

    public IFileProvider WebRootFileProvider
    {
        get => new PhysicalFileProvider(Directory.Exists(WebRootPath) ? WebRootPath : ContentRootPath);
        set { }
    }

    /// <summary>Creates <c>{temp}/nh-{prefix}-{guid}</c> with a <c>wwwroot</c> child.</summary>
    public static TestWebHostEnvironment CreateTemp(string prefix)
    {
        var root = Path.Combine(Path.GetTempPath(), $"nh-{prefix}-" + Guid.NewGuid().ToString("N"));
        var web = Path.Combine(root, "wwwroot");
        Directory.CreateDirectory(web);
        return new TestWebHostEnvironment(root, web);
    }
}

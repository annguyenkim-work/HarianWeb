using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using NewHarian.Infrastructure.Media;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Tests;

public class MediaPathAndPrivateStorageTests
{
    [Fact]
    public void Private_prefix_resolves_under_App_Data_not_wwwroot()
    {
        var root = Path.Combine(Path.GetTempPath(), "nh-media-" + Guid.NewGuid().ToString("N"));
        var web = Path.Combine(root, "wwwroot");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(web);
        var env = new FakeEnv(root, web);

        var abs = MediaPathResolver.GetAbsolutePath(env, "private/applications/abc.pdf", isPrivate: true);
        Assert.StartsWith(Path.Combine(root, "App_Data"), abs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Path.Combine("wwwroot", "uploads"), abs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveDocumentAsync_writes_outside_wwwroot()
    {
        var root = Path.Combine(Path.GetTempPath(), "nh-media-" + Guid.NewGuid().ToString("N"));
        var web = Path.Combine(root, "wwwroot");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(web);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new AppDbContext(options);
        var env = new FakeEnv(root, web);
        var storage = new LocalMediaStorage(db, env, NullLogger<LocalMediaStorage>.Instance);

        await using var stream = new MemoryStream("%PDF-1.4 cv-bytes"u8.ToArray());
        var result = await storage.SaveDocumentAsync(stream, "resume.pdf", "application/pdf", null);

        var row = await db.MediaFiles.FindAsync(result.Id);
        Assert.NotNull(row);
        Assert.True(row!.IsPrivate);
        Assert.StartsWith("private/", row.StoredPath);

        var abs = MediaPathResolver.GetAbsolutePath(env, row);
        Assert.True(File.Exists(abs));
        Assert.False(File.Exists(Path.Combine(web, "uploads", "applications", Path.GetFileName(row.StoredPath))));
        Assert.Equal(string.Empty, result.Url);
    }

    private sealed class FakeEnv : IWebHostEnvironment
    {
        public FakeEnv(string contentRoot, string webRoot)
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
    }
}

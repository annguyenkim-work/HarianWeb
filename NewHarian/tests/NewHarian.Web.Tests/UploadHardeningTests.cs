using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using NewHarian.Application.Abstractions;
using NewHarian.Infrastructure.Media;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Tests;

public class UploadHardeningTests
{
    [Fact]
    public void Detects_jpeg_png_pdf_signatures()
    {
        Assert.Equal(DetectedFileKind.Jpeg, FileSignatureMatcher.Detect(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));
        Assert.Equal(DetectedFileKind.Png, FileSignatureMatcher.Detect(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }));
        Assert.Equal(DetectedFileKind.Pdf, FileSignatureMatcher.Detect("%PDF"u8.ToArray()));
    }

    [Fact]
    public async Task Rejects_extension_mismatch()
    {
        await using var scope = await CreateStorageAsync();
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        await using var stream = new MemoryStream(png);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.Storage.SaveImageAsync(stream, "fake.jpg", "image/jpeg", null));
        Assert.Contains("không khớp", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_exe_renamed_as_pdf()
    {
        await using var scope = await CreateStorageAsync();
        var mz = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };
        await using var stream = new MemoryStream(mz);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.Storage.SaveDocumentAsync(stream, "virus.pdf", "application/pdf", null));
        Assert.Contains("PDF", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_oversized_payload()
    {
        await using var scope = await CreateStorageAsync();
        var bytes = new byte[MediaUploadLimits.MaxFileBytes + 100];
        "%PDF-1.4"u8.CopyTo(bytes);
        await using var stream = new MemoryStream(bytes);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.Storage.SaveDocumentAsync(stream, "big.pdf", "application/pdf", null));
        Assert.Contains("10MB", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Accepts_valid_png_image()
    {
        await using var scope = await CreateStorageAsync();
        var png = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0, 0, 0, 0, 0, 0, 0, 0
        };
        await using var stream = new MemoryStream(png);
        var result = await scope.Storage.SaveImageAsync(stream, "ok.png", "image/png", null, folder: "products");
        Assert.True(result.Id > 0);
        Assert.StartsWith("/uploads/", result.Url);
    }

    private static async Task<StorageScope> CreateStorageAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "nh-upload-" + Guid.NewGuid().ToString("N"));
        var web = Path.Combine(root, "wwwroot");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(web);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var env = new FakeEnv(root, web);
        var storage = new LocalMediaStorage(db, env, NullLogger<LocalMediaStorage>.Instance);
        return new StorageScope(storage, db, root);
    }

    private sealed class StorageScope(LocalMediaStorage storage, AppDbContext db, string root) : IAsyncDisposable
    {
        public LocalMediaStorage Storage { get; } = storage;
        public async ValueTask DisposeAsync()
        {
            await db.DisposeAsync();
            try { Directory.Delete(root, recursive: true); } catch { /* best effort */ }
        }
    }

    private sealed class FakeEnv : IWebHostEnvironment
    {
        public FakeEnv(string contentRoot, string webRoot)
        {
            ContentRootPath = contentRoot;
            WebRootPath = webRoot;
            ContentRootFileProvider = new PhysicalFileProvider(contentRoot);
        }

        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider
        {
            get => new PhysicalFileProvider(WebRootPath);
            set { }
        }
    }
}

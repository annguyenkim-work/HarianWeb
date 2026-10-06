using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NewHarian.Infrastructure.Media;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Tests;

public class MediaPathAndPrivateStorageTests
{
    [Fact]
    public async Task SaveDocumentAsync_writes_outside_wwwroot()
    {
        var env = TestWebHostEnvironment.CreateTemp("media");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new AppDbContext(options);
        var storage = new LocalMediaStorage(db, env, NullLogger<LocalMediaStorage>.Instance);

        try
        {
            await using var stream = new MemoryStream("%PDF-1.4 cv-bytes"u8.ToArray());
            var result = await storage.SaveDocumentAsync(stream, "resume.pdf", "application/pdf", null);

            var row = await db.MediaFiles.FindAsync(result.Id);
            Assert.NotNull(row);
            Assert.True(row!.IsPrivate);
            Assert.StartsWith("private/", row.StoredPath);

            var abs = MediaPathResolver.GetAbsolutePath(env, row);
            Assert.True(File.Exists(abs));
            Assert.False(File.Exists(Path.Combine(env.WebRootPath, "uploads", "applications", Path.GetFileName(row.StoredPath))));
            Assert.Equal(string.Empty, result.Url);
        }
        finally
        {
            try { Directory.Delete(env.ContentRootPath, recursive: true); } catch { /* best effort */ }
        }
    }
}

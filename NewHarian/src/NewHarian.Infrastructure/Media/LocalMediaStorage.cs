using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Media;

public class LocalMediaStorage(AppDbContext db, IWebHostEnvironment env, ILogger<LocalMediaStorage> logger) : IMediaStorage
{
    private static readonly HashSet<DetectedFileKind> ImageKinds =
        [DetectedFileKind.Jpeg, DetectedFileKind.Png, DetectedFileKind.Gif, DetectedFileKind.Webp];

    private static readonly HashSet<DetectedFileKind> DocumentKinds =
    [
        DetectedFileKind.Pdf, DetectedFileKind.Doc, DetectedFileKind.Docx,
        DetectedFileKind.Jpeg, DetectedFileKind.Png
    ];

    public Task<MediaUploadResult> SaveImageAsync(
        Stream content, string fileName, string contentType, string? uploadedByUserId, CancellationToken ct = default,
        string folder = "products")
        => SaveAsync(content, fileName, uploadedByUserId, folder, ImageKinds,
            "Chỉ chấp nhận ảnh JPG, PNG, WEBP, GIF.", isPrivate: false, ct);

    public Task<MediaUploadResult> SaveDocumentAsync(
        Stream content, string fileName, string contentType, string? uploadedByUserId, CancellationToken ct = default,
        string folder = "applications")
        => SaveAsync(content, fileName, uploadedByUserId, folder, DocumentKinds,
            "Chỉ chấp nhận PDF, DOC, DOCX, JPG, PNG.", isPrivate: true, ct);

    public async Task<MediaOpenResult?> OpenAsync(int mediaFileId, CancellationToken ct = default)
    {
        var media = await db.MediaFiles.FindAsync([mediaFileId], ct);
        if (media is null) return null;

        var absPath = MediaPathResolver.GetAbsolutePath(env, media);
        if (!File.Exists(absPath))
        {
            logger.LogWarning("OpenMedia missing Id={Id} Path={Path}", mediaFileId, absPath);
            return null;
        }

        var allowedRoots = new[]
        {
            Path.Combine(env.ContentRootPath, "App_Data"),
            env.WebRootPath
        };
        if (!allowedRoots.Any(root => MediaPathResolver.IsUnderRoot(absPath, root)))
        {
            logger.LogWarning("OpenMedia path rejected Id={Id} Path={Path}", mediaFileId, absPath);
            return null;
        }

        Stream stream = File.OpenRead(absPath);
        return new MediaOpenResult(stream, media.ContentType, media.FileName);
    }

    private async Task<MediaUploadResult> SaveAsync(
        Stream content, string fileName, string? uploadedByUserId, string folder,
        HashSet<DetectedFileKind> allowedKinds, string typeError, bool isPrivate, CancellationToken ct)
    {
        logger.LogInformation("SaveMedia Start FileName={FileName} Folder={Folder} Private={Private}", fileName, folder, isPrivate);
        try
        {
            await using var buffered = await ReadWithSizeLimitAsync(content, MediaUploadLimits.MaxFileBytes, ct);
            if (buffered.Length == 0)
                throw new InvalidOperationException("File rỗng.");

            var headerLen = (int)Math.Min(16, buffered.Length);
            Span<byte> header = stackalloc byte[headerLen];
            buffered.Position = 0;
            _ = buffered.Read(header);
            buffered.Position = 0;

            var kind = FileSignatureMatcher.Detect(header);
            if (kind == DetectedFileKind.Unknown || !allowedKinds.Contains(kind))
                throw new InvalidOperationException(typeError);

            // ZIP magic is used for DOCX — require matching extension so random ZIPs are not accepted as resumes.
            var ext = Path.GetExtension(fileName)?.ToLowerInvariant() ?? "";
            if (kind == DetectedFileKind.Docx && ext != ".docx")
                throw new InvalidOperationException(typeError);
            if (!string.IsNullOrEmpty(ext) && !FileSignatureMatcher.ExtensionMatches(kind, ext))
                throw new InvalidOperationException(
                    $"Nội dung file không khớp phần mở rộng ({ext}). Vui lòng tải đúng định dạng.");

            if (string.IsNullOrEmpty(ext))
                ext = FileSignatureMatcher.PreferredExtension(kind);

            var detectedContentType = FileSignatureMatcher.ContentTypeOf(kind);
            folder = string.IsNullOrWhiteSpace(folder) ? "uploads" : folder.Trim().Trim('/');
            var storedName = $"{Guid.NewGuid():N}{ext}";

            string storedPath;
            string absDir;
            if (isPrivate)
            {
                storedPath = MediaPathResolver.ToPrivateStoredPath(folder, storedName);
                absDir = Path.Combine(env.ContentRootPath, "App_Data", "private", folder);
            }
            else
            {
                storedPath = MediaPathResolver.ToPublicUrl(folder, storedName);
                absDir = Path.Combine(env.WebRootPath, "uploads", folder);
            }

            Directory.CreateDirectory(absDir);
            var absPath = Path.Combine(absDir, storedName);
            await using (var fs = File.Create(absPath))
            {
                buffered.Position = 0;
                await buffered.CopyToAsync(fs, ct);
            }

            var media = new MediaFile
            {
                FileName = Path.GetFileName(fileName),
                StoredPath = storedPath,
                ContentType = detectedContentType,
                FileSizeBytes = buffered.Length,
                UploadedByUserId = uploadedByUserId,
                IsPrivate = isPrivate,
                CreatedAt = DateTime.UtcNow
            };
            db.MediaFiles.Add(media);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("SaveMedia Done Id={Id} Path={Path} Bytes={Bytes} Kind={Kind}",
                media.Id, media.StoredPath, media.FileSizeBytes, kind);
            return new MediaUploadResult(media.Id, isPrivate ? string.Empty : media.StoredPath, media.FileName);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger.LogError(ex, "SaveMedia Error FileName={FileName} Folder={Folder}", fileName, folder);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("SaveMedia Done rejected FileName={FileName} Error={Error}", fileName, ex.Message);
            throw;
        }
    }

    private static async Task<MemoryStream> ReadWithSizeLimitAsync(Stream content, long maxBytes, CancellationToken ct)
    {
        var ms = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                await ms.DisposeAsync();
                throw new InvalidOperationException($"File tối đa {MediaUploadLimits.MaxFileLabel}.");
            }
            await ms.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        ms.Position = 0;
        return ms;
    }
}

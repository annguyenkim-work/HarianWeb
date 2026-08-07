using Microsoft.AspNetCore.Hosting;
using NewHarian.Domain.Entities;

namespace NewHarian.Infrastructure.Media;

/// <summary>
/// Resolves MediaFile.StoredPath to an absolute filesystem path.
/// Public images: wwwroot/uploads/...
/// Private docs: ContentRoot/App_Data/private/...
/// Legacy private files remain under wwwroot/uploads/applications until resolved/moved.
/// </summary>
public static class MediaPathResolver
{
    public const string PrivatePrefix = "private/";
    public const string PublicUploadsPrefix = "/uploads/";

    public static string ToPublicUrl(string folder, string storedName)
        => $"{PublicUploadsPrefix.TrimEnd('/')}/{folder.Trim('/')}/{storedName}";

    public static string ToPrivateStoredPath(string folder, string storedName)
        => $"{PrivatePrefix}{folder.Trim('/')}/{storedName}";

    public static string GetAbsolutePath(IWebHostEnvironment env, MediaFile media)
        => GetAbsolutePath(env, media.StoredPath, media.IsPrivate);

    public static string GetAbsolutePath(IWebHostEnvironment env, string storedPath, bool isPrivate)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
            throw new InvalidOperationException("StoredPath is empty.");

        var normalized = storedPath.Replace('\\', '/').Trim();

        if (normalized.StartsWith(PrivatePrefix, StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(Path.Combine(env.ContentRootPath, "App_Data", normalized.Replace('/', Path.DirectorySeparatorChar)));

        // Legacy public URL form "/uploads/..."
        if (normalized.StartsWith(PublicUploadsPrefix, StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
        {
            var relative = normalized.TrimStart('/');
            return Path.GetFullPath(Path.Combine(env.WebRootPath, relative.Replace('/', Path.DirectorySeparatorChar)));
        }

        // Fallback: treat as relative under App_Data when flagged private
        if (isPrivate)
            return Path.GetFullPath(Path.Combine(env.ContentRootPath, "App_Data", normalized.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));

        return Path.GetFullPath(Path.Combine(env.WebRootPath, normalized.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
    }

    public static bool IsUnderRoot(string absolutePath, string rootDirectory)
    {
        var full = Path.GetFullPath(absolutePath);
        var root = Path.GetFullPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar)
                   + Path.DirectorySeparatorChar;
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}

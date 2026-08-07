namespace NewHarian.Application.Abstractions;

/// <summary>Central upload size policy (business max 10MB; HTTP slightly larger for multipart).</summary>
public static class MediaUploadLimits
{
    public const long MaxFileBytes = 10 * 1024 * 1024;
    public const long HttpRequestBytes = 12 * 1024 * 1024;
    public const string MaxFileLabel = "10MB";
}

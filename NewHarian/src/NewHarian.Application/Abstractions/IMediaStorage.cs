namespace NewHarian.Application.Abstractions;

public record MediaUploadResult(int Id, string Url, string FileName);

public record MediaOpenResult(Stream Content, string ContentType, string DownloadFileName) : IAsyncDisposable, IDisposable
{
    public void Dispose() => Content.Dispose();
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public interface IMediaStorage
{
    Task<MediaUploadResult> SaveImageAsync(
        Stream content,
        string fileName,
        string contentType,
        string? uploadedByUserId,
        CancellationToken ct = default,
        string folder = "products");

    Task<MediaUploadResult> SaveDocumentAsync(
        Stream content,
        string fileName,
        string contentType,
        string? uploadedByUserId,
        CancellationToken ct = default,
        string folder = "applications");

    /// <summary>Opens a stored media file stream (used for private CV download / email attach).</summary>
    Task<MediaOpenResult?> OpenAsync(int mediaFileId, CancellationToken ct = default);
}

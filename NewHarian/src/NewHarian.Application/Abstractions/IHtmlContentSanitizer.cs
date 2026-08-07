namespace NewHarian.Application.Abstractions;

/// <summary>Server-side HTML allowlist sanitizer for CMS / product rich text.</summary>
public interface IHtmlContentSanitizer
{
    string? Sanitize(string? html);
}

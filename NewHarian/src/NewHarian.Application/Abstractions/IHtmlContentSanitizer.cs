namespace NewHarian.Application.Abstractions;

/// <summary>Sanitizes CMS / product rich text: keep layout and media, strip script XSS.</summary>
public interface IHtmlContentSanitizer
{
    string? Sanitize(string? html);
}

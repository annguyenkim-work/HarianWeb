using Ganss.Xss;
using NewHarian.Application.Abstractions;

namespace NewHarian.Infrastructure.Security;

public sealed class HtmlContentSanitizer : IHtmlContentSanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    public HtmlContentSanitizer()
    {
        _sanitizer = new HtmlSanitizer();
        _sanitizer.AllowedTags.Clear();
        foreach (var tag in new[]
                 {
                     "p", "br", "hr", "div", "span",
                     "h1", "h2", "h3", "h4",
                     "strong", "b", "em", "i", "u", "s", "sub", "sup",
                     "ul", "ol", "li",
                     "a", "img",
                     "blockquote", "pre", "code",
                     "table", "thead", "tbody", "tr", "th", "td"
                 })
            _sanitizer.AllowedTags.Add(tag);

        _sanitizer.AllowedAttributes.Clear();
        foreach (var attr in new[]
                 {
                     "href", "src", "alt", "title", "class", "target", "rel",
                     "width", "height", "colspan", "rowspan"
                 })
            _sanitizer.AllowedAttributes.Add(attr);

        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("mailto");

        _sanitizer.AllowDataAttributes = false;
    }

    public string? Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;
        var cleaned = _sanitizer.Sanitize(html);
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }
}

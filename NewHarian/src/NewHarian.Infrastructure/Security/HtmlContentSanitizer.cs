using Ganss.Xss;
using NewHarian.Application.Abstractions;

namespace NewHarian.Infrastructure.Security;

/// <summary>
/// Allows rich CMS HTML (layout, video, inline CSS) while still stripping script/handlers/javascript URLs.
/// </summary>
public sealed class HtmlContentSanitizer : IHtmlContentSanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    public HtmlContentSanitizer()
    {
        _sanitizer = new HtmlSanitizer();

        foreach (var tag in new[]
                 {
                     "iframe", "video", "audio", "source", "track", "picture",
                     "section", "article", "header", "footer", "nav", "aside", "main",
                     "figure", "figcaption", "button", "style",
                     "svg", "path", "g", "circle", "rect", "line", "polyline", "polygon",
                     "use", "defs", "clippath", "title", "desc",
                     "h5", "h6", "small", "mark", "abbr", "cite", "time", "address",
                     "colgroup", "col", "tfoot", "caption"
                 })
            _sanitizer.AllowedTags.Add(tag);

        foreach (var tag in new[] { "script", "noscript", "object", "embed", "applet", "form", "input", "textarea", "select", "option", "link", "meta", "base" })
            _sanitizer.AllowedTags.Remove(tag);

        foreach (var attr in new[]
                 {
                     "style", "class", "id", "name", "type", "value", "role", "tabindex",
                     "allow", "allowfullscreen", "allowtransparency", "frameborder", "scrolling",
                     "loading", "referrerpolicy", "srcset", "sizes", "media", "crossorigin",
                     "controls", "autoplay", "loop", "muted", "playsinline", "poster", "preload",
                     "aria-label", "aria-hidden", "aria-labelledby",
                     "viewbox", "fill", "stroke", "stroke-width", "d", "cx", "cy", "r", "x", "y",
                     "x1", "y1", "x2", "y2", "points", "transform", "xmlns"
                 })
            _sanitizer.AllowedAttributes.Add(attr);

        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("mailto");

        _sanitizer.AllowDataAttributes = true;
        _sanitizer.AllowedClasses.Clear();

        foreach (var css in new[]
                 {
                     "display", "flex", "flex-direction", "flex-wrap", "flex-grow", "flex-shrink", "flex-basis",
                     "justify-content", "align-items", "align-content", "align-self", "order", "gap",
                     "row-gap", "column-gap", "place-items", "place-content", "place-self",
                     "grid", "grid-template-columns", "grid-template-rows", "grid-template-areas",
                     "grid-column", "grid-row", "grid-area", "grid-gap", "grid-auto-flow",
                     "width", "height", "min-width", "min-height", "max-width", "max-height",
                     "margin", "margin-top", "margin-right", "margin-bottom", "margin-left",
                     "padding", "padding-top", "padding-right", "padding-bottom", "padding-left",
                     "background", "background-color", "background-image", "background-size",
                     "background-position", "background-repeat", "background-clip",
                     "border", "border-top", "border-right", "border-bottom", "border-left",
                     "border-radius", "border-color", "border-style", "border-width",
                     "box-shadow", "box-sizing", "overflow", "overflow-x", "overflow-y",
                     "color", "font", "font-family", "font-size", "font-weight", "font-style",
                     "line-height", "letter-spacing", "text-align", "text-decoration", "text-transform",
                     "white-space", "word-break", "opacity", "visibility",
                     "position", "top", "right", "bottom", "left", "z-index", "inset",
                     "object-fit", "object-position", "aspect-ratio",
                     "transform", "transition", "filter", "backdrop-filter",
                     "cursor", "list-style", "vertical-align"
                 })
            _sanitizer.AllowedCssProperties.Add(css);

        _sanitizer.AllowedAtRules.Add(AngleSharp.Css.Dom.CssRuleType.Media);
        _sanitizer.AllowedAtRules.Add(AngleSharp.Css.Dom.CssRuleType.Supports);
        _sanitizer.AllowedAtRules.Add(AngleSharp.Css.Dom.CssRuleType.Keyframes);
        _sanitizer.AllowedAtRules.Add(AngleSharp.Css.Dom.CssRuleType.Keyframe);
    }

    public string? Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;
        var cleaned = _sanitizer.Sanitize(html);
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }
}

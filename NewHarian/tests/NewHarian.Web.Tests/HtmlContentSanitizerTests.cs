using NewHarian.Infrastructure.Security;

namespace NewHarian.Web.Tests;

public class HtmlContentSanitizerTests
{
    private readonly HtmlContentSanitizer _sut = new();

    [Fact]
    public void Removes_script_tags_and_event_handlers()
    {
        var dirty = "<p>Hello</p><script>alert(1)</script><img src=x onerror=alert(2) /><a href=\"javascript:alert(3)\">x</a>";
        var clean = _sut.Sanitize(dirty);

        Assert.NotNull(clean);
        Assert.DoesNotContain("<script", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", clean, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hello", clean, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_youtube_iframe_and_video()
    {
        var html = """
            <p>Clip</p>
            <iframe src="https://www.youtube.com/embed/abc" allow="accelerometer; autoplay" allowfullscreen></iframe>
            <video controls src="https://cdn.example.com/a.mp4"></video>
            """;
        var clean = _sut.Sanitize(html);
        Assert.Contains("<iframe", clean, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("youtube.com", clean, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<video", clean, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Keeps_inline_layout_styles()
    {
        var html = """<div class="hero" style="display: flex; gap: 12px; border-radius: 16px; background: #f5ead7;">Top 1</div>""";
        var clean = _sut.Sanitize(html);
        Assert.Contains("display", clean, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("flex", clean, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("border-radius", clean, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hero", clean, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_safe_formatting_tags()
    {
        var html = "<p><strong>Bold</strong> and <em>italic</em></p><ul><li>One</li></ul>";
        var clean = _sut.Sanitize(html);
        Assert.Contains("<strong>", clean);
        Assert.Contains("<em>", clean);
        Assert.Contains("<li>", clean);
    }
}

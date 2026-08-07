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
    public void Keeps_safe_formatting_tags()
    {
        var html = "<p><strong>Bold</strong> and <em>italic</em></p><ul><li>One</li></ul>";
        var clean = _sut.Sanitize(html);
        Assert.Contains("<strong>", clean);
        Assert.Contains("<em>", clean);
        Assert.Contains("<li>", clean);
    }
}
